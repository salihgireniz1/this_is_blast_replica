// LevelSpawner - builds the level on screen and stays its view registry.
// Layer: Presentation.
// Responsibility: instantiating a CubeView per board cube and a ShooterView per queued
//   shooter at the world position its address maps to - and, from then on, answering
//   which view stands at which front and moving the survivors when a front falls. It
//   mirrors the domain's nothing-moves bookkeeping: views stay in their authored lists
//   and a front index walks forward.
// NOT its responsibility: gameplay decisions or pacing. GameDirector decides WHEN a view
//   is popped, killed or stepped; this type only knows WHERE everything is, because it
//   owns the layout numbers.
// NOT its responsibility: finding its dependencies. GameLifetimeScope hands them in
//   through Construct - the humble-object seam that keeps this class free of lookups.
//
// The layout numbers are serialized, not computed from renderer bounds: the grid must
// match GameArea's authored size (its inner floor is 9.5 x 9.5 around the origin, so ten
// 0.95 cells span it exactly), and reading bounds at runtime would re-derive at startup
// what is a design-time fact - and drift the moment someone swaps the art.

using System.Collections.Generic;
using Blast.Domain;
using DG.Tweening;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Spawns the level's views and tracks who stands where as it is played.</summary>
    public sealed class LevelSpawner : MonoBehaviour
    {
        #region Fields

        /// <summary>The cube visual to instantiate per board cell.</summary>
        [Tooltip("The CubeView prefab spawned per board cell.")]
        [SerializeField] CubeView _cubePrefab;

        /// <summary>The shooter visual to instantiate per queued shooter.</summary>
        [Tooltip("The ShooterView prefab spawned per queued shooter.")]
        [SerializeField] ShooterView _shooterPrefab;

        /// <summary>World position of the front-left cube's centre.</summary>
        [Tooltip("World centre of the front-left cube (column 0, front row, ground layer). Move this to move the whole board.")]
        [SerializeField] Vector3 _boardOrigin = new Vector3(-4.275f, 0.45f, -4.275f);

        /// <summary>Distance between neighbouring cube centres; ten cells span GameArea's 9.5.</summary>
        [Tooltip("World units between neighbouring cube centres, on every axis. 0.95 x 10 spans GameArea's 9.5 floor exactly.")]
        [SerializeField] float _cellSize = 0.95f;

        /// <summary>World position of the front shooter of the leftmost queue column.</summary>
        [Tooltip("World centre of the queue's front row. Columns spread left and right of it, deeper rows go further back.")]
        [SerializeField] Vector3 _queueOrigin = new Vector3(0f, 0f, -7.5f);

        /// <summary>Distance between neighbouring queue columns; the row centres on x = 0.</summary>
        [Tooltip("World units between neighbouring queue columns.")]
        [SerializeField] float _queueSpacingX = 1.5f;

        /// <summary>Distance between queue rows, walking away from the board.</summary>
        [Tooltip("World units between queue rows, going away from the board.")]
        [SerializeField] float _queueSpacingZ = 1.2f;

        /// <summary>Depth of the slot row, between the board and the queue.</summary>
        [Tooltip("Z of the slot row. Sits between the board's front row and the queue.")]
        [SerializeField] float _slotZ = -5.9f;

        /// <summary>Distance between neighbouring slots; the row centres on x = 0.</summary>
        [Tooltip("World units between neighbouring slots. The row centres on x = 0.")]
        [SerializeField] float _slotSpacingX = 1.5f;

        /// <summary>The board the cubes come from. Handed in by Construct.</summary>
        BoardModel _board;

        /// <summary>The queue the shooters come from. Handed in by Construct.</summary>
        ShooterQueue _shooters;

        /// <summary>How many slots the level's row holds, for centring their positions.</summary>
        int _slotCount;

        /// <summary>The colour table the views dress from. Handed in by Construct.</summary>
        IColorMaterials _materials;

        /// <summary>Each board column's cube views in row order; the front index walks forward.</summary>
        List<CubeView>[] _cubeColumns;

        /// <summary>Each board column's first view still standing.</summary>
        int[] _cubeFront;

        /// <summary>How many cells each board column has flowed forward. Counted, never
        /// read off a transform: a move target derived from a mid-tween position is short
        /// by whatever the tween had left to run.</summary>
        int[] _cubeFlowed;

        /// <summary>Each queue column's shooter views in authored order; the front index walks forward.</summary>
        List<ShooterView>[] _queueColumns;

        /// <summary>Each queue column's first view still waiting.</summary>
        int[] _queueFront;

        #endregion

        #region Public Methods

        /// <summary>Receives the level and the colour table, then builds the scene.</summary>
        /// <param name="board">The board to spawn cubes for.</param>
        /// <param name="shooters">The queue to spawn shooters for.</param>
        /// <param name="slots">The slot row, for how many slot positions to lay out.</param>
        /// <param name="materials">The colour table the views dress from.</param>
        public void Construct(BoardModel board, ShooterQueue shooters, SlotRow slots, IColorMaterials materials)
        {
            _board = board;
            _shooters = shooters;
            _slotCount = slots.Slots;
            _materials = materials;

            SpawnCubes();
            SpawnShooters();
        }

        /// <summary>The world position of a slot's centre.</summary>
        /// <param name="slot">The slot index, left to right.</param>
        public Vector3 SlotWorldPosition(int slot)
        {
            float centeredSlot = slot - (_slotCount - 1) * 0.5f;

            return new Vector3(centeredSlot * _slotSpacingX, 0f, _slotZ);
        }

        /// <summary>Whether a view is the selectable front of its queue column.</summary>
        /// <param name="view">The view the player tapped.</param>
        /// <param name="column">That view's column, when it is a front.</param>
        public bool TryGetSelectableColumn(ShooterView view, out int column)
        {
            for (column = 0; column < _queueColumns.Length; column++)
            {
                bool columnHasAFront = _queueFront[column] < _queueColumns[column].Count;

                if (columnHasAFront && _queueColumns[column][_queueFront[column]] == view)
                {
                    return true;
                }
            }

            column = -1;
            return false;
        }

        /// <summary>Hands out a queue column's front view and advances the view front.</summary>
        /// <param name="column">The column whose front was selected.</param>
        public ShooterView PopFrontShooter(int column)
        {
            ShooterView front = _queueColumns[column][_queueFront[column]];
            _queueFront[column]++;

            return front;
        }

        /// <summary>Steps a column's waiting views one row up and reveals the new front.</summary>
        /// <param name="column">The column that just lost its front.</param>
        /// <param name="duration">How long the step takes.</param>
        public void StepQueueForward(int column, float duration)
        {
            List<ShooterView> views = _queueColumns[column];

            for (int index = _queueFront[column]; index < views.Count; index++)
            {
                Transform view = views[index].transform;
                int depth = index - _queueFront[column];

                // Same rule as the board's flow: an absolute target, and the previous
                // step's tween killed so two selections in one breath cannot end short.
                view.DOKill();
                view.DOMove(QueueWorldPosition(column, depth), duration);
            }

            // The domain's front has already advanced (the director selects before it
            // steps the views), so depth 0 is the shooter arriving at the selectable row -
            // the exact moment the Hidden Shooter rule says its colour may show.
            bool columnStillHasShooters = _shooters.Remaining(column) > 0;
            if (columnStillHasShooters)
            {
                Shooter front = _shooters.Peek(column, 0);
                views[_queueFront[column]].ShowRevealed(_materials.MaterialOf(front.Color), front.Ammo);
            }
        }

        /// <summary>Hands out a board column's front cube view and advances the view front.</summary>
        /// <param name="column">The board column whose front cube died.</param>
        public CubeView PopFrontCube(int column)
        {
            CubeView front = _cubeColumns[column][_cubeFront[column]];
            _cubeFront[column]++;

            return front;
        }

        /// <summary>Flows a board column's surviving cubes one cell toward the player, each
        /// landing with a small overshoot-and-bounce.</summary>
        /// <param name="column">The column that just lost its front.</param>
        /// <param name="duration">How long the slide takes.</param>
        /// <param name="settleDistance">How far a cube overshoots its cell before bouncing back.</param>
        /// <param name="settleDuration">How long that bounce takes.</param>
        /// <returns>The slide the director may await before the column is shootable again;
        /// null when nothing was left to move.</returns>
        public Tween FlowBoardColumn(int column, float duration, float settleDistance, float settleDuration)
        {
            List<CubeView> views = _cubeColumns[column];
            int flowed = ++_cubeFlowed[column];
            Tween slide = null;

            for (int index = _cubeFront[column]; index < views.Count; index++)
            {
                Transform view = views[index].transform;
                Vector3 authored = CubeWorldPosition(CellOfCubeView(column, index));
                Vector3 rest = authored - new Vector3(0f, 0f, flowed * _cellSize);

                // Kill first: the previous flow's tween still holds the previous rest as
                // its target and would drag the cube back when it lands. A landing bounce
                // cut short by this kill is healed the same way: the next slide targets the
                // absolute rest, wherever the bounce left the cube.
                view.DOKill();
                slide = view.DOMove(rest, duration)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() => view
                        .DOPunchPosition(Vector3.back * settleDistance, settleDuration, vibrato: 2, elasticity: 0.5f)
                        .SetEase(Ease.OutBounce));
                // ponytail: the OnComplete closure allocates per cube per shot; a pooled
                // Sequence is the upgrade if the phase 5 profiler flags it.
            }

            // Every survivor slides for the same duration, so the last one's tween stands
            // for the whole column's landing.
            return slide;
        }

        #endregion

        #region Private Methods

        /// <summary>Places one CubeView per board cell, coloured as authored.</summary>
        void SpawnCubes()
        {
            _cubeColumns = new List<CubeView>[_board.Columns];
            _cubeFront = new int[_board.Columns];
            _cubeFlowed = new int[_board.Columns];

            for (int column = 0; column < _board.Columns; column++)
            {
                _cubeColumns[column] = new List<CubeView>(_board.Rows * _board.Layers);

                for (int row = 0; row < _board.Rows; row++)
                for (int layer = 0; layer < _board.Layers; layer++)
                {
                    Cell cell = new Cell(column, row, layer);
                    CubeView cube = Instantiate(_cubePrefab, CubeWorldPosition(cell), Quaternion.identity, transform);

                    cube.Wear(_materials.MaterialOf(_board.Get(cell)));
                    _cubeColumns[column].Add(cube);
                }
            }
        }

        /// <summary>Places one ShooterView per queued shooter, concealed where the rule says so.</summary>
        void SpawnShooters()
        {
            _queueColumns = new List<ShooterView>[_shooters.Columns];
            _queueFront = new int[_shooters.Columns];

            for (int column = 0; column < _shooters.Columns; column++)
            {
                _queueColumns[column] = new List<ShooterView>(_shooters.Remaining(column));

                for (int depth = 0; depth < _shooters.Remaining(column); depth++)
                {
                    Vector3 position = QueueWorldPosition(column, depth);
                    ShooterView view = Instantiate(_shooterPrefab, position, Quaternion.identity, transform);
                    Shooter shooter = _shooters.Peek(column, depth);

                    if (_shooters.IsRevealed(column, depth))
                    {
                        view.ShowRevealed(_materials.MaterialOf(shooter.Color), shooter.Ammo);
                    }
                    else
                    {
                        view.ShowConcealed(_materials.HiddenMaterial);
                    }

                    _queueColumns[column].Add(view);
                }
            }
        }

        /// <summary>Maps a column's list index back to the address the view was spawned at.</summary>
        /// <param name="column">The board column the view belongs to.</param>
        /// <param name="index">Its position in that column's list, as SpawnCubes filled it.</param>
        Cell CellOfCubeView(int column, int index)
        {
            int row = index / _board.Layers;
            int layer = index % _board.Layers;

            return new Cell(column, row, layer);
        }

        /// <summary>Maps a board address to the world position of its cube's centre.</summary>
        /// <param name="cell">The address to map.</param>
        Vector3 CubeWorldPosition(Cell cell)
        {
            // Row 0 is the front row: the board edge nearest the slots, so rows walk away
            // from the camera (+z) while queue rows walk toward it (-z).
            return _boardOrigin + new Vector3(cell.Column * _cellSize, cell.Layer * _cellSize, cell.Row * _cellSize);
        }

        /// <summary>Maps a queue address to the world position of its shooter.</summary>
        /// <param name="column">The queue column, left to right.</param>
        /// <param name="depth">How far behind the front; zero is the selectable row.</param>
        Vector3 QueueWorldPosition(int column, int depth)
        {
            // Columns centre on the origin's x so any column count sits symmetrically.
            float centeredColumn = column - (_shooters.Columns - 1) * 0.5f;

            return _queueOrigin + new Vector3(centeredColumn * _queueSpacingX, 0f, -depth * _queueSpacingZ);
        }

        #endregion
    }
}
