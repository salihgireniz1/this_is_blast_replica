// Shooter - one cannon as authored: what it fires, how much, and whether it hides it.
// Layer: Domain (engine-free).
// Responsibility: carrying a shooter's authored facts - colour, ammo, hidden flag -
//   unchanged from the level file to whoever asks.
// NOT its responsibility: whether the colour may be shown right now. Concealment is a
//   question about where the shooter stands in its queue, so ShooterQueue answers it;
//   folding a mutable "revealed" flag in here would let two copies of the same shooter
//   disagree about it.
// NOT its responsibility: spending ammo. Ammo only starts moving once the shooter sits in
//   a slot and fires, which is the slot row's behaviour and lands with that chunk.

namespace Blast.Domain
{
    /// <summary>One shooter as the level authored it.</summary>
    public readonly struct Shooter
    {
        #region Properties

        /// <summary>The colour of cube this shooter destroys.</summary>
        public BlastColor Color { get; }

        /// <summary>How many cubes it can destroy before it leaves.</summary>
        public int Ammo { get; }

        /// <summary>Whether the colour stays concealed until the shooter reaches the front row.</summary>
        public bool IsHidden { get; }

        #endregion

        #region Public Methods

        /// <summary>Builds a shooter from its authored facts.</summary>
        /// <param name="color">The colour it fires.</param>
        /// <param name="ammo">How many shots it carries.</param>
        /// <param name="isHidden">Whether the colour is concealed until the front row.</param>
        public Shooter(BlastColor color, int ammo, bool isHidden)
        {
            Color = color;
            Ammo = ammo;
            IsHidden = isHidden;
        }

        #endregion
    }
}
