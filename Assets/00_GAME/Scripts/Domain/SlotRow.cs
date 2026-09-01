// SlotRow - the middle of the scene: the slots a selected shooter runs to and fires from.
// Layer: Domain (engine-free).
// Responsibility: seating each arriving shooter in the first empty slot, spending its ammo
//   shot by shot, and freeing the slot the moment the last shot is gone - so a shooter with
//   no ammo can never be found occupying a slot.
// NOT its responsibility: the firing rule. Which cube a shooter may shoot, and whether it
//   has a target at all, is answered above this type; a shooter with ammo but nothing to
//   shoot simply never gets a Spend call and keeps its slot - which is exactly the pressure
//   the fail condition reads through IsFull.
// NOT its responsibility: how many slots exist. The case fixes it at five, but that is the
//   level file's fact to state; this type takes the count and holds no magic number.
//
// Occupancy is the ammo itself: a slot with shots left is occupied, a slot at zero is
// empty. One array of counts instead of a parallel bool[] that could disagree with it.

using System;

namespace Blast.Domain
{
    /// <summary>The row of slots shooters fire from, filled front-to-back and freed by ammo.</summary>
    public sealed class SlotRow
    {
        #region Fields

        /// <summary>The shooter standing in each slot; meaningful only while its ammo is above zero.</summary>
        readonly Shooter[] _shooters;

        /// <summary>Each slot's remaining shots; zero means the slot is empty.</summary>
        readonly int[] _ammo;

        #endregion

        #region Properties

        /// <summary>How many slots the row holds.</summary>
        public int Slots => _ammo.Length;

        /// <summary>Whether every slot is occupied - the slot half of the fail condition.</summary>
        public bool IsFull
        {
            get
            {
                for (int slot = 0; slot < _ammo.Length; slot++)
                {
                    if (_ammo[slot] == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>Builds an empty row of the given size.</summary>
        /// <param name="slots">How many slots the row holds.</param>
        public SlotRow(int slots)
        {
            _shooters = new Shooter[slots];
            _ammo = new int[slots];
        }

        /// <summary>Whether a slot currently holds a shooter.</summary>
        /// <param name="slot">The slot to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        public bool IsOccupied(int slot)
        {
            ValidateSlot(slot);

            return _ammo[slot] > 0;
        }

        /// <summary>Reads the shooter standing in a slot.</summary>
        /// <param name="slot">The slot to read.</param>
        /// <returns>The shooter standing there.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        /// <exception cref="InvalidOperationException">The slot is empty.</exception>
        public Shooter ShooterAt(int slot)
        {
            ValidateOccupied(slot);

            return _shooters[slot];
        }

        /// <summary>Reads how many shots a slot's shooter still carries.</summary>
        /// <param name="slot">The slot to read.</param>
        /// <returns>The remaining shots; zero for an empty slot.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        public int AmmoAt(int slot)
        {
            ValidateSlot(slot);

            return _ammo[slot];
        }

        /// <summary>Seats a shooter in the first empty slot.</summary>
        /// <param name="shooter">The shooter that ran over.</param>
        /// <returns>The slot it took.</returns>
        /// <exception cref="InvalidOperationException">Every slot is occupied.</exception>
        public int Occupy(Shooter shooter)
        {
            for (int slot = 0; slot < _ammo.Length; slot++)
            {
                bool slotIsEmpty = _ammo[slot] == 0;
                if (slotIsEmpty)
                {
                    _shooters[slot] = shooter;
                    _ammo[slot] = shooter.Ammo;

                    return slot;
                }
            }

            throw new InvalidOperationException(
                "Every slot is occupied; the fail condition should have been asked first.");
        }

        /// <summary>Spends one shot from a slot, freeing it when that was the last.</summary>
        /// <param name="slot">The slot whose shooter fired.</param>
        /// <returns>The shots remaining after this one.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        /// <exception cref="InvalidOperationException">The slot is empty.</exception>
        public int Spend(int slot)
        {
            ValidateOccupied(slot);

            int remaining = _ammo[slot] - 1;
            _ammo[slot] = remaining;

            return remaining;
        }

        #endregion

        #region Private Methods

        /// <summary>Refuses a slot that does not exist.</summary>
        /// <param name="slot">The slot to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        void ValidateSlot(int slot)
        {
            // Same uint cast as the other Domain types: one comparison covers both ends.
            if ((uint)slot >= (uint)_ammo.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot),
                    $"Slot {slot} does not exist in a row of {_ammo.Length}.");
            }
        }

        /// <summary>Refuses an empty slot where a standing shooter is required.</summary>
        /// <param name="slot">The slot to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
        /// <exception cref="InvalidOperationException">The slot is empty.</exception>
        void ValidateOccupied(int slot)
        {
            ValidateSlot(slot);

            // Without this, spending from an empty slot walks the marker to -1 and the slot
            // reads as occupied again - nothing would ever throw on its own.
            if (_ammo[slot] == 0)
            {
                throw new InvalidOperationException($"Slot {slot} is empty.");
            }
        }

        #endregion
    }
}
