// LevelFileTests - the shipped level files against the case brief's requirements.
// Layer: Tests (EditMode).
// Responsibility: the level-content mistakes that only surface mid-game - a file that no
//   longer parses, a colour whose shooters carry less ammo than it has cubes (unwinnable,
//   and visibly so only at the very end of a playthrough), a level missing one of the five
//   required colours, or one that lost its hidden shooter.
// NOT its responsibility: the parser's behaviour (LevelParserTests) or full solvability.
//   Ammo >= cubes per colour is necessary, not sufficient - ordering deadlocks are proven
//   away by playing the level, which the case expects anyway.
// The five-colour and hidden-shooter asserts are the case brief's requirements for the
// shipped sample level and are scoped to it (SampleLevel): a three-colour level with nothing
// hidden elsewhere is a design, not a mistake. Parsing, ammo >= cubes and the column cap
// apply to every file - those break the game, not the brief.

using System.Collections.Generic;
using Blast.Domain;
using Blast.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>Validates every shipped level file in Assets/00_GAME/Levels.</summary>
    public sealed class LevelFileTests
    {
        #region Fields

        /// <summary>Where shipped level files live - also the future HTML editor's export target.</summary>
        const string LevelsFolder = "Assets/00_GAME/Levels";

        /// <summary>
        /// How many shooter columns fit on screen. The queue is centred on x = 0 at the same
        /// spacing as the five slots, so a sixth column starts leaving the frame and its
        /// shooters spawn where nobody can tap them. The brief leaves the count to the level
        /// (Not3); the dock's width is what caps it.
        /// </summary>
        const int MaxQueueColumns = 5;

        /// <summary>The level the scene boots and the case delivers; the only file the five-colour rule applies to.</summary>
        const string SampleLevel = "Level_01.json";

        /// <summary>The five colours the case requires the sample level to use.</summary>
        static readonly BlastColor[] RequiredColors =
        {
            BlastColor.Yellow, BlastColor.Red, BlastColor.Blue, BlastColor.Green, BlastColor.Orange,
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Every shipped level parses, uses all five colours, carries a hidden shooter, and
        /// arms every colour with at least as much ammo as it has cubes. Under-ammo is the
        /// silent one: the level plays fine for minutes and becomes unwinnable at the end.
        /// </summary>
        [Test]
        public void EveryShippedLevel_MeetsTheCaseBrief()
        {
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder });

            Assert.IsNotEmpty(guids, $"No level files found under {LevelsFolder}.");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var level = LevelParser.Parse(file.text);

                var cubes = CountCubes(level.Board);
                var ammo = CountAmmo(level.Shooters, out bool anyHidden);

                // All five colours and a hidden shooter are the brief's requirements for the
                // sample level alone; a three-colour level with nothing hidden is a
                // legitimate design anywhere else.
                if (System.IO.Path.GetFileName(path) == SampleLevel)
                {
                    foreach (var color in RequiredColors)
                    {
                        Assert.IsTrue(cubes.ContainsKey(color),
                            $"{path}: the board uses no {color} cube; the case requires all five colours in the sample level.");
                    }

                    Assert.IsTrue(anyHidden,
                        $"{path}: no hidden shooter; the case requires the feature in the sample level.");
                }

                foreach (var pair in cubes)
                {
                    Assert.GreaterOrEqual(ammo.GetValueOrDefault(pair.Key), pair.Value,
                        $"{path}: {pair.Key} has {pair.Value} cubes but only " +
                        $"{ammo.GetValueOrDefault(pair.Key)} ammo; the level cannot be won.");
                }

                Assert.LessOrEqual(level.Shooters.Columns, MaxQueueColumns,
                    $"{path}: {level.Shooters.Columns} shooter columns, and only {MaxQueueColumns} " +
                    "fit across the dock; the rest spawn off-screen where nobody can tap them.");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>Counts the cubes of each colour on a freshly parsed board.</summary>
        /// <param name="board">The board to count.</param>
        static Dictionary<BlastColor, int> CountCubes(BoardModel board)
        {
            var counts = new Dictionary<BlastColor, int>();

            for (int column = 0; column < board.Columns; column++)
            for (int row = 0; row < board.Rows; row++)
            for (int layer = 0; layer < board.Layers; layer++)
            {
                var color = board.Get(new Cell(column, row, layer));
                counts[color] = counts.GetValueOrDefault(color) + 1;
            }

            return counts;
        }

        /// <summary>Sums each colour's ammo across the queue, noting any hidden shooter.</summary>
        /// <param name="shooters">The queue to sum.</param>
        /// <param name="anyHidden">Whether at least one shooter is authored hidden.</param>
        static Dictionary<BlastColor, int> CountAmmo(ShooterQueue shooters, out bool anyHidden)
        {
            var totals = new Dictionary<BlastColor, int>();
            anyHidden = false;

            for (int column = 0; column < shooters.Columns; column++)
            {
                for (int depth = 0; depth < shooters.Remaining(column); depth++)
                {
                    var shooter = shooters.Peek(column, depth);
                    totals[shooter.Color] = totals.GetValueOrDefault(shooter.Color) + shooter.Ammo;
                    anyHidden |= shooter.IsHidden;
                }
            }

            return totals;
        }

        #endregion
    }
}
