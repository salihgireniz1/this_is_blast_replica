// LevelDefinition - the level file's shape, exactly as JsonUtility needs to see it.
// Layer: Infrastructure.
// Responsibility: mirroring the JSON schema field for field so JsonUtility can fill it.
//   This is a dumb transport shape: no validation, no domain types, no behaviour.
// NOT its responsibility: being correct. A freshly deserialized instance may hold nulls,
//   zeros and nonsense - LevelParser is the gate that refuses those. Nothing outside the
//   parser should ever touch this type.
//
// The fields are lowercase on purpose, breaking the PascalCase rule: JsonUtility maps
// strictly by field name and the JSON keys are lowercase. Renaming either side breaks
// every level file, the future HTML level editor included - this type IS the contract.

using System;

namespace Blast.Infrastructure
{
    /// <summary>The raw deserialized shape of a level file.</summary>
    [Serializable]
    public sealed class LevelDefinition
    {
        #region Fields

        /// <summary>One string per board row, front row first, one colour letter per column.</summary>
        public string[] boardRows;

        /// <summary>How many slots the level's slot row holds.</summary>
        public int slotCount;

        /// <summary>The shooter columns, left to right.</summary>
        public ShooterColumnDefinition[] shooterColumns;

        #endregion

        #region Nested Types

        /// <summary>One column of shooters, front first.</summary>
        [Serializable]
        public sealed class ShooterColumnDefinition
        {
            /// <summary>The column's shooters; the first is the selectable front.</summary>
            public ShooterDefinition[] shooters;
        }

        /// <summary>One shooter's authored facts, still in file form.</summary>
        [Serializable]
        public sealed class ShooterDefinition
        {
            /// <summary>The colour letter, same alphabet as the board rows.</summary>
            public string color;

            /// <summary>How many shots it carries.</summary>
            public int ammo;

            /// <summary>Whether the colour stays concealed until the front row.</summary>
            public bool hidden;
        }

        #endregion
    }
}
