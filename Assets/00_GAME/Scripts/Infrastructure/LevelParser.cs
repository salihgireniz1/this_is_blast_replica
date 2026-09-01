// LevelParser - the trust boundary: a level file goes in, playable domain models come out.
// Layer: Infrastructure.
// Responsibility: deserializing the JSON, refusing everything malformed with a message
//   that names the problem, and building the BoardModel, ShooterQueue and SlotRow exactly
//   as authored - boardRows[0] is the front row, each queue column is front-first.
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

            BoardModel board = BuildBoard(definition.boardRows);
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

        /// <summary>Builds the board from the row strings, front row first.</summary>
        /// <param name="boardRows">One string per row, one colour letter per column.</param>
        /// <exception cref="FormatException">The rows are missing, empty or ragged.</exception>
        static BoardModel BuildBoard(string[] boardRows)
        {
            if (boardRows == null || boardRows.Length == 0)
            {
                throw new FormatException("The level has no boardRows.");
            }

            int columns = boardRows[0].Length;
            var board = new BoardModel(columns, boardRows.Length, layers: 1);

            for (int row = 0; row < boardRows.Length; row++)
            {
                string rowLetters = boardRows[row];

                if (rowLetters.Length != columns)
                {
                    throw new FormatException(
                        $"boardRows[{row}] has {rowLetters.Length} letters where the first row " +
                        $"has {columns}; every row must be the same width.");
                }

                for (int column = 0; column < columns; column++)
                {
                    BlastColor color = ColorOfLetter(rowLetters[column], $"boardRows[{row}][{column}]");
                    board.Set(new Cell(column, row, 0), color);
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
