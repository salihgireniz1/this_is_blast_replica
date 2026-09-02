// LevelParserTests - the trust boundary between a level file and the domain.
// Layer: Tests (EditMode).
// Responsibility: the mistakes a parser makes silently - a grid built transposed or
//   upside down, a queue built back-to-front, an authored slot count ignored, and bad
//   data (unknown letter, ragged rows, zero ammo, missing sections) patched over instead
//   of refused with a message.
// NOT its responsibility: the domain types' own behaviour once built - their suites cover
//   that. The fixtures here are deliberately asymmetric (no two dimensions equal, no
//   repeated colour pattern) so a transposed or reversed read cannot pass by luck.

using System;
using Blast.Domain;
using Blast.Infrastructure;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="LevelParser"/>'s construction and refusal rules.</summary>
    public sealed class LevelParserTests
    {
        #region Fields

        /// <summary>
        /// A 3x2x2 level with every part deliberately asymmetric: rows differ from columns,
        /// row 0 differs from row 1, the upper layer differs from the ground layer cell for
        /// cell, and the two queue columns differ in length.
        /// </summary>
        const string ValidLevel = @"{
            ""boardLayers"": [
                { ""rows"": [""YRB"", ""GOR""] },
                { ""rows"": [""OBG"", ""RYO""] }
            ],
            ""slotCount"": 2,
            ""shooterColumns"": [
                { ""shooters"": [
                    { ""color"": ""R"", ""ammo"": 5, ""hidden"": false },
                    { ""color"": ""B"", ""ammo"": 3, ""hidden"": true }
                ]},
                { ""shooters"": [
                    { ""color"": ""G"", ""ammo"": 1, ""hidden"": false }
                ]}
            ]
        }";

        #endregion

        #region Public Methods

        /// <summary>
        /// The board comes out exactly as authored: rows[0] of the ground layer is the front row and each
        /// string reads left to right across the columns. A transposed or upside-down build
        /// still fills every cell, so only spot checks on an asymmetric grid can tell.
        /// </summary>
        [Test]
        public void Parse_BuildsTheBoardAsAuthored()
        {
            var level = LevelParser.Parse(ValidLevel);

            Assert.AreEqual(3, level.Board.Columns, "The column count did not come from the row length.");
            Assert.AreEqual(2, level.Board.Rows, "The row count did not come from the row list.");
            Assert.AreEqual(BlastColor.Yellow, level.Board.Get(new Cell(0, 0, 0)),
                "The first letter of the first row is not at column 0 of the front row.");
            Assert.AreEqual(BlastColor.Blue, level.Board.Get(new Cell(2, 0, 0)),
                "The last letter of the first row is not at the last column of the front row.");
            Assert.AreEqual(BlastColor.Orange, level.Board.Get(new Cell(1, 1, 0)),
                "The grid is transposed or upside down.");
        }

        /// <summary>
        /// Layers come out ground first: boardLayers[0] is the layer on the floor and each
        /// later entry stacks on top of it. Swapped layers put the wrong colour on top of
        /// every stack, which is the colour the first shot has to match.
        /// </summary>
        [Test]
        public void Parse_BuildsTheLayersGroundFirst()
        {
            var level = LevelParser.Parse(ValidLevel);

            Assert.AreEqual(2, level.Board.Layers, "The layer count did not come from the layer list.");
            Assert.AreEqual(BlastColor.Orange, level.Board.Get(new Cell(0, 0, 1)),
                "The second layer's first letter is not on top of the front-left cell.");
            Assert.AreEqual(BlastColor.Yellow, level.Board.Get(new Cell(1, 1, 1)),
                "The upper layer is transposed, upside down or read through the ground layer.");
        }

        /// <summary>
        /// A layer with a different row count is refused - a shallower upper layer would
        /// leave its missing rows reading as Yellow, the enum's zero, without a word.
        /// </summary>
        [Test]
        public void Parse_RefusesLayersOfDifferentDepth()
        {
            var broken = ValidLevel.Replace(@"[""OBG"", ""RYO""]", @"[""OBG""]");

            Assert.Throws<FormatException>(() => LevelParser.Parse(broken),
                "A layer shallower than the ground layer was accepted.");
        }

        /// <summary>
        /// Each queue column comes out front-first with its authored facts. A reversed build
        /// hands the hidden shooter out first, which the player sees as the wrong reveal.
        /// </summary>
        [Test]
        public void Parse_BuildsTheQueuesFrontFirst()
        {
            var level = LevelParser.Parse(ValidLevel);

            Assert.AreEqual(2, level.Shooters.Columns, "The queue column count is wrong.");

            var front = level.Shooters.Peek(0, 0);
            var behind = level.Shooters.Peek(0, 1);

            Assert.AreEqual(BlastColor.Red, front.Color, "Column 0's queue is not front-first.");
            Assert.AreEqual(5, front.Ammo, "The front shooter's ammo was not carried over.");
            Assert.IsTrue(behind.IsHidden, "The authored hidden flag was dropped.");
        }

        /// <summary>
        /// The slot row is the authored size. A parser that hardcodes the case's five would
        /// pass every playthrough of a five-slot level and quietly ignore the file.
        /// </summary>
        [Test]
        public void Parse_BuildsTheSlotRowTheFileAsksFor()
        {
            var level = LevelParser.Parse(ValidLevel);

            Assert.AreEqual(2, level.Slots.Slots, "slotCount was not read from the file.");
        }

        /// <summary>An unknown colour letter is refused, not defaulted to some colour.</summary>
        [Test]
        public void Parse_RefusesAnUnknownColourLetter()
        {
            var broken = ValidLevel.Replace("YRB", "YXB");

            Assert.Throws<FormatException>(() => LevelParser.Parse(broken),
                "An unknown colour letter was quietly turned into a colour.");
        }

        /// <summary>Rows of different lengths are refused - the grid would silently skew.</summary>
        [Test]
        public void Parse_RefusesRaggedRows()
        {
            var broken = ValidLevel.Replace("\"GOR\"", "\"GO\"");

            Assert.Throws<FormatException>(() => LevelParser.Parse(broken),
                "A ragged grid was accepted; every row after the short one lands shifted.");
        }

        /// <summary>
        /// A shooter with no ammo is refused at the boundary. SlotRow treats zero ammo as an
        /// empty slot, so letting one through would seat a shooter that instantly vanishes.
        /// </summary>
        [Test]
        public void Parse_RefusesAShooterWithoutAmmo()
        {
            var broken = ValidLevel.Replace("\"ammo\": 5", "\"ammo\": 0");

            Assert.Throws<FormatException>(() => LevelParser.Parse(broken),
                "A zero-ammo shooter slipped through the boundary.");
        }

        /// <summary>A file missing its board is refused - JsonUtility hands back nulls, not errors.</summary>
        [Test]
        public void Parse_RefusesAFileWithoutABoard()
        {
            Assert.Throws<FormatException>(() => LevelParser.Parse("{}"),
                "An empty file was accepted; JsonUtility's silent nulls reached the domain.");
        }

        #endregion
    }
}
