// LevelParser - the trust boundary: a level file goes in, playable domain models come out.
// Layer: Infrastructure.
// Responsibility: deserializing the JSON, refusing everything malformed with a message
//   that names the problem, and building the BoardModel, ShooterQueue and SlotRow exactly
//   as authored - boardLayers[0] is the ground layer, rows[0] is the front row, each queue
//   column is front-first.
// NOT its responsibility: fixing a broken file. Every refusal throws FormatException and
//   nothing is ever defaulted or skipped: a level that parses is a level that was authored
//   correctly, and the future HTML level editor gets the same verdicts on its output.
//
// Why the checks are this paranoid: JsonUtility never fails on missing keys - it hands
// back nulls and zeros and calls it success. Every one of those silent defaults would
// otherwise surface as a NullReferenceException deep in gameplay, far from the file that
// caused it.

using System;
using Blast.Domain;
using UnityEngine;

namespace Blast.Infrastructure
{
    /// <summary>Parses a JSON level file into playable domain models.</summary>
    public static class LevelParser
    {
        #region Public Methods

        /// <summary>Parses a level file's text into its domain models.</summary>
        /// <param name="json">The level file's content.</param>
        /// <returns>The board, queue and slot row, ready to play.</returns>
        /// <exception cref="FormatException">The file is malformed; the message names how.</exception>
        public static ParsedLevel Parse(string json)
        {
            LevelDefinition definition = JsonUtility.FromJson<LevelDefinition>(json);

            BoardModel board = BuildBoard(definition.boardLayers);
            ShooterQueue shooters = BuildQueue(definition.shooterColumns);

            if (definition.slotCount < 1)
            {
                throw new FormatException(
                    $"slotCount is {definition.slotCount}; a level needs at least one slot.");
            }

            SlotRow slots = new SlotRow(definition.slotCount);

            return new ParsedLevel(board, shooters, slots);
        }

        #endregion

        #region Private Methods

        /// <summary>Builds the board from the layers, ground layer first and front row first.</summary>
        /// <param name="boardLayers">One grid of row strings per layer, ground first.</param>
        /// <exception cref="FormatException">A layer or row is missing, empty or ragged.</exception>
        static BoardModel BuildBoard(LevelDefinition.BoardLayerDefinition[] boardLayers)
        {
            if (boardLayers == null || boardLayers.Length == 0)
            {
                throw new FormatException("The level has no boardLayers.");
            }

            string[] groundRows = boardLayers[0].rows;

            if (groundRows == null || groundRows.Length == 0)
            {
                throw new FormatException("boardLayers[0] has no rows.");
            }

            // The ground layer sets the board's size; every other layer must match it, because
            // BoardModel is a filled box - a cell it was never told about reads as Yellow.
            int columns = groundRows[0].Length;
            int rows = groundRows.Length;
            var board = new BoardModel(columns, rows, boardLayers.Length);

            for (int layer = 0; layer < boardLayers.Length; layer++)
            {
                string[] layerRows = boardLayers[layer].rows;
                int layerDepth = layerRows == null ? 0 : layerRows.Length;

                if (layerDepth != rows)
                {
                    throw new FormatException(
                        $"boardLayers[{layer}] has {layerDepth} rows where the ground layer has " +
                        $"{rows}; every layer must be the same depth.");
                }

                for (int row = 0; row < rows; row++)
                {
                    string rowLetters = layerRows[row];
                    string where = $"boardLayers[{layer}].rows[{row}]";

                    if (rowLetters == null || rowLetters.Length != columns)
                    {
                        throw new FormatException(
                            $"{where} has {rowLetters?.Length ?? 0} letters where the ground " +
                            $"layer's first row has {columns}; every row must be the same width.");
                    }

                    for (int column = 0; column < columns; column++)
                    {
                        BlastColor color = ColorOfLetter(rowLetters[column], $"{where}[{column}]");
                        board.Set(new Cell(column, row, layer), color);
                    }
                }
            }

            return board;
        }

        /// <summary>Builds the shooter queue from the authored columns, front first.</summary>
        /// <param name="shooterColumns">The columns as the file authored them.</param>
        /// <exception cref="FormatException">A column is missing, empty or holds a bad shooter.</exception>
        static ShooterQueue BuildQueue(LevelDefinition.ShooterColumnDefinition[] shooterColumns)
        {
            if (shooterColumns == null || shooterColumns.Length == 0)
            {
                throw new FormatException("The level has no shooterColumns.");
            }

            var columns = new Shooter[shooterColumns.Length][];

            for (int column = 0; column < shooterColumns.Length; column++)
            {
                LevelDefinition.ShooterDefinition[] authored = shooterColumns[column].shooters;

                if (authored == null || authored.Length == 0)
                {
                    throw new FormatException($"shooterColumns[{column}] has no shooters.");
                }

                columns[column] = new Shooter[authored.Length];

                for (int position = 0; position < authored.Length; position++)
                {
                    columns[column][position] =
                        BuildShooter(authored[position], $"shooterColumns[{column}].shooters[{position}]");
                }
            }

            return new ShooterQueue(columns);
        }

        /// <summary>Builds one shooter from its file form.</summary>
        /// <param name="authored">The shooter as the file authored it.</param>
        /// <param name="where">The file location, for refusal messages.</param>
        /// <exception cref="FormatException">The colour or ammo is unusable.</exception>
        static Shooter BuildShooter(LevelDefinition.ShooterDefinition authored, string where)
        {
            if (string.IsNullOrEmpty(authored.color) || authored.color.Length != 1)
            {
                throw new FormatException(
                    $"{where} has colour '{authored.color}'; expected a single letter.");
            }

            // SlotRow reads zero ammo as an empty slot, so a zero-ammo shooter would be
            // seated and instantly vanish. Refuse it here, where the file can be blamed.
            if (authored.ammo < 1)
            {
                throw new FormatException(
                    $"{where} has ammo {authored.ammo}; a shooter needs at least one shot.");
            }

            BlastColor color = ColorOfLetter(authored.color[0], where);

            return new Shooter(color, authored.ammo, authored.hidden);
        }

        /// <summary>Maps a colour letter to its <see cref="BlastColor"/>.</summary>
        /// <param name="letter">The letter to map.</param>
        /// <param name="where">The file location, for refusal messages.</param>
        /// <exception cref="FormatException">The letter is not in the alphabet.</exception>
        static BlastColor ColorOfLetter(char letter, string where)
        {
            switch (letter)
            {
                case 'Y': return BlastColor.Yellow;
                case 'R': return BlastColor.Red;
                case 'B': return BlastColor.Blue;
                case 'G': return BlastColor.Green;
                case 'O': return BlastColor.Orange;
                default:
                    throw new FormatException(
                        $"{where} holds '{letter}'; the colour letters are Y, R, B, G, O.");
            }
        }

        #endregion
    }
}
