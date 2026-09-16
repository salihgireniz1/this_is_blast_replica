// LevelSpawner - builds the level on screen and stays its view registry.
// Layer: Presentation.
// Responsibility: instantiating a CubeView per board cube and a ShooterView per queued
//   shooter at the world position its address maps to - and, from then on, answering
//   which view stands at which front and moving the survivors when a front falls. It
//   mirrors the domain's nothing-moves bookkeeping: views stay in their authored lists
//   and a front index walks forward. The lists run top layer first within each row, because
//   that is the order BoardModel.Remove hands cubes out; a row's survivors flow only once
//   its whole stack is gone, which the front index says by landing on a row boundary.
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

using System;
using System.Collections.Generic;
using Blast.Domain;
using DG.Tweening;
using UnityEngine;
using VContainer;

namespace Blast.Presentation
{
    /// <summary>Spawns the level's views and tracks who stands where as it is played.</summary>
    public sealed class LevelSpawner : MonoBehaviour
    {
        #region Fields

        /// <summary>What to instantiate for a cube and for a shooter. One inspector heading.</summary>
        [Tooltip("The two view prefabs the level is built from.")]
        [SerializeField] Prefabs _prefabs;

        /// <summary>Where the board sits and how big a cell is. One inspector heading.</summary>
        [Tooltip("Where the board sits and how far apart its cubes are.")]
        [SerializeField] BoardLayout _boardLayout = BoardLayout.Defaults;

        /// <summary>Where the shooter queue sits and how it spreads. One inspector heading.</summary>
        [Tooltip("Where the shooter queue sits and how its columns and rows spread.")]
        [SerializeField] QueueLayout _queueLayout = QueueLayout.Defaults;

        /// <summary>Where the slot row sits and how it spreads. One inspector heading.</summary>
        [Tooltip("Where seated shooters stand and how far apart.")]
        [SerializeField] SlotLayout _slotLayout = SlotLayout.Defaults;

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
        [Inject]
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

            return new Vector3(centeredSlot * _slotLayout.SpacingX, 0f, _slotLayout.Z);
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

            // A front tapped while still stepping up arrives now: its pending step lands,
            // its arrival reveal fires with it, and only then is the outline taken off. So
            // a hidden shooter never leaves for its slot in grey, and the reveal cannot
            // outline a shooter that is already seated.
            DOTween.Complete(front.transform, withCallbacks: true);
            _queueFront[column]++;
            front.SetOutlined(false);

            return front;
        }

        /// <summary>Steps a column's waiting views one row up; the new front is revealed and outlined when it arrives.</summary>
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
                Tween step = view.DOMove(QueueWorldPosition(column, depth), duration);

                // The Hidden Shooter rule timed as the brief words it: the colour shows when
                // the shooter REACHES the selectable row, so the reveal rides on the front's
                // arrival rather than on the tap. One closure per selection - a tap, not a
                // frame, the same budget SetOutlined spends.
                if (depth == 0)
                {
                    // Read WHO arrives now, not on arrival. The domain's front is this
                    // shooter at this moment; a second tap before the step lands takes it
                    // too, and a read at arrival would then dress this view in the NEXT
                    // shooter's colour and ammo (a green hidden shooter revealing blue).
                    ShooterView arriving = views[index];
                    Shooter shooter = _shooters.Peek(column, 0);
                    step.OnComplete(() => Reveal(arriving, shooter));
                }
            }
        }

        /// <summary>How many board columns there are; the director sweeps them while a bullet flies.</summary>
        public int BoardColumns => _board.Columns;

        /// <summary>The board's cell size: the footprint a bullet must enter to brush a cube.</summary>
        public float CellSize => _boardLayout.CellSize;

        /// <summary>Reads a board column's front cube view without advancing the front.</summary>
        /// <param name="column">The board column to look at.</param>
        /// <param name="cube">The view the next pop would hand out, when the column still stands.</param>
        /// <returns>False when the column has no cube left.</returns>
        public bool TryPeekFrontCube(int column, out CubeView cube)
        {
            List<CubeView> views = _cubeColumns[column];
            int front = _cubeFront[column];

            cube = front < views.Count ? views[front] : null;
            return cube != null;
        }

        /// <summary>
        /// The cube of a column's front stack nearest a height: on a layered board a bullet
        /// flies at its target's height, and the neighbour it brushes is the cube beside it,
        /// not the top of the stack. Ask only while <see cref="TryPeekFrontCube"/> says the column stands.
        /// </summary>
        /// <param name="column">The board column to look at.</param>
        /// <param name="height">The bullet's world height where it crosses the stack.</param>
        public CubeView FrontCubeNearest(int column, float height)
        {
            List<CubeView> views = _cubeColumns[column];
            int front = _cubeFront[column];

            // The front stack runs from the front index to the next row boundary, top layer
            // first; what was popped above the front is gone and never offered.
            int stackEnd = front + (_board.Layers - front % _board.Layers);
            CubeView nearest = null;
            float nearestGap = float.MaxValue;

            for (int index = front; index < stackEnd && index < views.Count; index++)
            {
                float gap = Mathf.Abs(views[index].transform.position.y - height);
                if (gap < nearestGap)
                {
                    nearestGap = gap;
                    nearest = views[index];
                }
            }

            return nearest;
        }

        /// <summary>Hands out a board column's front cube view and advances the view front.</summary>
        /// <param name="column">The board column whose front cube died.</param>
        public CubeView PopFrontCube(int column)
        {
            CubeView front = _cubeColumns[column][_cubeFront[column]];
            _cubeFront[column]++;

            return front;
        }

        /// <summary>Whether the front stack of a column still has cubes standing after the last
        /// pop. The front index walks Layers views per row, so anything but a row boundary
        /// means the row has not fallen yet and nothing behind it is going to move.</summary>
        /// <param name="column">The board column to ask about.</param>
        public bool StackStillStands(int column) => _cubeFront[column] % _board.Layers != 0;

        /// <summary>Flows a board column's surviving cubes one cell toward the player, each
        /// overshooting its cell a little and easing back: the original's landing, measured
        /// frame by frame (see CLAUDE.md).</summary>
        /// <param name="column">The column that just lost its front.</param>
        /// <param name="duration">How long the slide takes, overshoot and settle included.</param>
        /// <param name="overshoot">DOTween's OutBack overshoot: 1.7 is the default, ~10% of a cell past the rest.</param>
        /// <returns>The slide the director awaits before the column counts as settled;
        /// null when nothing was left to move.</returns>
        public Tween FlowBoardColumn(int column, float duration, float overshoot)
        {
            // A row falls only when its last cube dies.
            if (StackStillStands(column))
            {
                return null;
            }

            List<CubeView> views = _cubeColumns[column];
            int flowed = ++_cubeFlowed[column];
            Tween slide = null;

            for (int index = _cubeFront[column]; index < views.Count; index++)
            {
                Vector3 authored = CubeWorldPosition(CellOfCubeView(column, index));
                Vector3 rest = authored - new Vector3(0f, 0f, flowed * _boardLayout.CellSize);

                // The rest is absolute, so a slide re-aimed mid-overshoot simply continues
                // to the next cell from wherever the cube is; the view re-targets its one
                // tween rather than building another.
                slide = views[index].SlideTo(rest, duration, overshoot);
            }

            // Every survivor slides for the same duration, so the last one's tween stands
            // for the whole column's landing.
            return slide;
        }

        #endregion

        #region Private Methods

        /// <summary>Dresses a view in its shooter's colour and ammo and outlines it: it has just arrived at the selectable row.</summary>
        /// <param name="view">The view that finished stepping up.</param>
        /// <param name="shooter">The shooter it stands for, read when its step began.</param>
        void Reveal(ShooterView view, Shooter shooter)
        {
            view.ShowRevealed(_materials.MaterialOf(shooter.Color), shooter.Ammo);
            view.SetOutlined(true);
        }

        /// <summary>Makes an empty child to spawn one kind of view under, so the hierarchy reads by kind.</summary>
        /// <param name="name">What the child is called.</param>
        Transform NewGroup(string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(transform, false);

            return group;
        }

        /// <summary>Places one CubeView per board cell, coloured as authored.</summary>
        void SpawnCubes()
        {
            _cubeColumns = new List<CubeView>[_board.Columns];
            _cubeFront = new int[_board.Columns];
            _cubeFlowed = new int[_board.Columns];
            Transform home = NewGroup("Cubes");

            for (int column = 0; column < _board.Columns; column++)
            {
                _cubeColumns[column] = new List<CubeView>(_board.Rows * _board.Layers);

                // Top layer first within a row: the domain removes a stack from the top down,
                // and PopFrontCube must hand out the same cube the domain just removed.
                for (int row = 0; row < _board.Rows; row++)
                for (int layer = _board.Layers - 1; layer >= 0; layer--)
                {
                    Cell cell = new Cell(column, row, layer);
                    CubeView cube = Instantiate(_prefabs.Cube, CubeWorldPosition(cell), Quaternion.identity, home);

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
            Transform home = NewGroup("Shooters");

            for (int column = 0; column < _shooters.Columns; column++)
            {
                _queueColumns[column] = new List<ShooterView>(_shooters.Remaining(column));

                for (int depth = 0; depth < _shooters.Remaining(column); depth++)
                {
                    Vector3 position = QueueWorldPosition(column, depth);
                    ShooterView view = Instantiate(_prefabs.Shooter, position, Quaternion.identity, home);
                    Shooter shooter = _shooters.Peek(column, depth);

                    if (_shooters.IsRevealed(column, depth))
                    {
                        view.ShowRevealed(_materials.MaterialOf(shooter.Color), shooter.Ammo);
                    }
                    else
                    {
                        view.ShowConcealed(_materials.HiddenMaterial);
                    }

                    // The outline is the "you may tap this" mark, so only the front wears it.
                    view.SetOutlined(depth == 0);
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
            int layer = _board.Layers - 1 - index % _board.Layers;

            return new Cell(column, row, layer);
        }

        /// <summary>Maps a board address to the world position of its cube's centre.</summary>
        /// <param name="cell">The address to map.</param>
        Vector3 CubeWorldPosition(Cell cell)
        {
            // Row 0 is the front row: the board edge nearest the slots, so rows walk away
            // from the camera (+z) while queue rows walk toward it (-z).
            return _boardLayout.Origin + new Vector3(cell.Column * _boardLayout.CellSize, cell.Layer * _boardLayout.CellSize, cell.Row * _boardLayout.CellSize);
        }

        /// <summary>Maps a queue address to the world position of its shooter.</summary>
        /// <param name="column">The queue column, left to right.</param>
        /// <param name="depth">How far behind the front; zero is the selectable row.</param>
        Vector3 QueueWorldPosition(int column, int depth)
        {
            // Columns centre on the origin's x so any column count sits symmetrically.
            float centeredColumn = column - (_shooters.Columns - 1) * 0.5f;

            return _queueLayout.Origin + new Vector3(centeredColumn * _queueLayout.SpacingX, 0f, -depth * _queueLayout.SpacingZ);
        }

        #endregion

        #region Nested Types

        /// <summary>The two view prefabs the level is built from. One inspector heading.</summary>
        [Serializable]
        public struct Prefabs
        {
            /// <summary>The cube visual to instantiate per board cell.</summary>
            [Tooltip("The CubeView prefab spawned per board cell.")]
            public CubeView Cube;

            /// <summary>The shooter visual to instantiate per queued shooter.</summary>
            [Tooltip("The ShooterView prefab spawned per queued shooter.")]
            public ShooterView Shooter;
        }

        /// <summary>Where the board sits and how big a cell is. One inspector heading.</summary>
        [Serializable]
        public struct BoardLayout
        {
            /// <summary>World position of the front-left cube's centre.</summary>
            [Tooltip("World centre of the front-left cube (column 0, front row, ground layer). Move this to move the whole board.")]
            public Vector3 Origin;

            /// <summary>Distance between neighbouring cube centres; ten cells span GameArea's 9.5.</summary>
            [Tooltip("World units between neighbouring cube centres, on every axis. 0.95 x 10 spans GameArea's 9.5 floor exactly.")]
            public float CellSize;

            /// <summary>The values a fresh spawner starts with: the scene's tuned numbers.</summary>
            public static BoardLayout Defaults => new BoardLayout
            {
                Origin = new Vector3(-4.275f, 0.45f, -5.775f),
                CellSize = 0.95f,
            };
        }

        /// <summary>Where the shooter queue sits and how it spreads. One inspector heading.</summary>
        [Serializable]
        public struct QueueLayout
        {
            /// <summary>World position the queue's front row centres on.</summary>
            [Tooltip("World centre of the queue's front row. Columns spread left and right of it, deeper rows go further back.")]
            public Vector3 Origin;

            /// <summary>Distance between neighbouring queue columns; the row centres on Origin.</summary>
            [Tooltip("World units between neighbouring queue columns.")]
            public float SpacingX;

            /// <summary>Distance between queue rows, walking away from the board.</summary>
            [Tooltip("World units between queue rows, going away from the board.")]
            public float SpacingZ;

            /// <summary>The values a fresh spawner starts with: the scene's tuned numbers.</summary>
            public static QueueLayout Defaults => new QueueLayout
            {
                Origin = new Vector3(0f, 0f, -14f),
                SpacingX = 2f,
                SpacingZ = 2.25f,
            };
        }

        /// <summary>Where the slot row sits and how it spreads. One inspector heading.</summary>
        [Serializable]
        public struct SlotLayout
        {
            /// <summary>Depth of the slot row, between the board and the queue.</summary>
            [Tooltip("Z of the slot row. Sits between the board's front row and the queue.")]
            public float Z;

            /// <summary>Distance between neighbouring slots; the row centres on x = 0.</summary>
            [Tooltip("World units between neighbouring slots. The row centres on x = 0.")]
            public float SpacingX;

            /// <summary>The values a fresh spawner starts with: the scene's tuned numbers.</summary>
            public static SlotLayout Defaults => new SlotLayout { Z = -10f, SpacingX = 2f };
        }

        #endregion
    }
}
