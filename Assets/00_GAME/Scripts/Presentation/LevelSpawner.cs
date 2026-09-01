// LevelSpawner - turns the parsed level into things on screen, once, at startup.
// Layer: Presentation.
// Responsibility: instantiating a CubeView per board cube and a ShooterView per queued
//   shooter, each at the world position its address maps to, dressed by the reveal rule.
// NOT its responsibility: gameplay. Nothing here moves, fires or dies - the game loop
//   drives that in a later chunk through the views this spawner creates.
// NOT its responsibility: finding its dependencies. GameLifetimeScope hands them in
//   through Construct - the humble-object seam that keeps this class free of lookups.
//
// The layout numbers are serialized, not computed from renderer bounds: the grid must
// match GameArea's authored size (its inner floor is 9.5 x 9.5 around the origin, so ten
// 0.95 cells span it exactly), and reading bounds at runtime would re-derive at startup
// what is a design-time fact - and drift the moment someone swaps the art.

using Blast.Domain;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Spawns the board's cubes and the queue's shooters at startup.</summary>
    public sealed class LevelSpawner : MonoBehaviour
    {
        #region Fields

        /// <summary>The cube visual to instantiate per board cell.</summary>
        [SerializeField] CubeView _cubePrefab;

        /// <summary>The shooter visual to instantiate per queued shooter.</summary>
        [SerializeField] ShooterView _shooterPrefab;

        /// <summary>World position of the front-left cube's centre.</summary>
        [SerializeField] Vector3 _boardOrigin = new Vector3(-4.275f, 0.45f, -4.275f);

        /// <summary>Distance between neighbouring cube centres; ten cells span GameArea's 9.5.</summary>
        [SerializeField] float _cellSize = 0.95f;

        /// <summary>World position of the front shooter of the leftmost queue column.</summary>
        [SerializeField] Vector3 _queueOrigin = new Vector3(0f, 0f, -7.5f);

        /// <summary>Distance between neighbouring queue columns; the row centres on x = 0.</summary>
        [SerializeField] float _queueSpacingX = 1.5f;

        /// <summary>Distance between queue rows, walking away from the board.</summary>
        [SerializeField] float _queueSpacingZ = 1.2f;

        /// <summary>The board the cubes come from. Handed in by Construct.</summary>
        BoardModel _board;

        /// <summary>The queue the shooters come from. Handed in by Construct.</summary>
        ShooterQueue _shooters;

        /// <summary>The colour table the views dress from. Handed in by Construct.</summary>
        IColorMaterials _materials;

        #endregion

        #region Public Methods

        /// <summary>Receives the level and the colour table, then builds the scene.</summary>
        /// <param name="board">The board to spawn cubes for.</param>
        /// <param name="shooters">The queue to spawn shooters for.</param>
        /// <param name="materials">The colour table the views dress from.</param>
        public void Construct(BoardModel board, ShooterQueue shooters, IColorMaterials materials)
        {
            _board = board;
            _shooters = shooters;
            _materials = materials;

            SpawnCubes();
            SpawnShooters();
        }

        #endregion

        #region Private Methods

        /// <summary>Places one CubeView per board cell, coloured as authored.</summary>
        void SpawnCubes()
        {
            for (int column = 0; column < _board.Columns; column++)
            for (int row = 0; row < _board.Rows; row++)
            for (int layer = 0; layer < _board.Layers; layer++)
            {
                Cell cell = new Cell(column, row, layer);
                CubeView cube = Instantiate(_cubePrefab, CubeWorldPosition(cell), Quaternion.identity, transform);

                cube.Wear(_materials.MaterialOf(_board.Get(cell)));
            }
        }

        /// <summary>Places one ShooterView per queued shooter, concealed where the rule says so.</summary>
        void SpawnShooters()
        {
            for (int column = 0; column < _shooters.Columns; column++)
            {
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
                }
            }
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
