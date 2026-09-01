// SlotRowTests - the slot row's occupancy discipline.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in SlotRow's source - a shooter sent to
//   the wrong empty slot, a slot still counted occupied after its last shot, a slot freed
//   a shot too early, a full row silently overwriting a standing shooter, and ammo spent
//   from a slot nobody stands in.
// NOT its responsibility: the firing rule (which cube a shooter may shoot) or the fail
//   condition itself. Those live above this type and get their own suites.

using System;
using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="SlotRow"/>'s occupy, spend and free rules.</summary>
    public sealed class SlotRowTests
    {
        #region Private Methods

        /// <summary>Builds a shooter carrying the given ammo.</summary>
        /// <param name="ammo">How many shots it carries.</param>
        static Shooter WithAmmo(int ammo) => new Shooter(BlastColor.Red, ammo, isHidden: false);

        #endregion

        #region Public Methods

        /// <summary>
        /// A shooter runs to the first empty slot, a freed slot included. An append-only
        /// implementation passes every fill-up test and only betrays itself when a slot in
        /// the middle empties and the next shooter must take it.
        /// </summary>
        [Test]
        public void Occupy_FillsTheFirstEmptySlot()
        {
            var row = new SlotRow(slots: 3);

            Assert.AreEqual(0, row.Occupy(WithAmmo(1)), "The first shooter skipped slot 0.");
            Assert.AreEqual(1, row.Occupy(WithAmmo(5)), "The second shooter skipped slot 1.");

            // Slot 0's single shot frees it; the next arrival must take slot 0, not slot 2.
            row.Spend(0);

            Assert.AreEqual(0, row.Occupy(WithAmmo(5)),
                "A freed slot was passed over; shooters run to the wrong slot.");
        }

        /// <summary>
        /// Spending the last shot frees the slot. A slot that stays occupied at zero ammo
        /// keeps a dead shooter in the row, and the fail condition ("all slots occupied")
        /// starts firing on rows that are actually half empty.
        /// </summary>
        [Test]
        public void SpendingTheLastShot_FreesTheSlot()
        {
            var row = new SlotRow(slots: 2);
            row.Occupy(WithAmmo(1));

            row.Spend(0);

            Assert.IsFalse(row.IsOccupied(0),
                "A shooter with no ammo left is still occupying its slot.");
        }

        /// <summary>
        /// Spending any shot but the last keeps the slot occupied. Freeing on every spend
        /// makes shooters leave after one shot regardless of ammo.
        /// </summary>
        [Test]
        public void SpendingBeforeTheLastShot_KeepsTheSlotOccupied()
        {
            var row = new SlotRow(slots: 2);
            row.Occupy(WithAmmo(2));

            row.Spend(0);

            Assert.IsTrue(row.IsOccupied(0),
                "A shooter with ammo left was thrown out of its slot.");
        }

        /// <summary>
        /// A full row refuses another shooter, and says so through IsFull first. Without the
        /// refusal an arrival overwrites whoever stands in the last slot and the row quietly
        /// loses a shooter.
        /// </summary>
        [Test]
        public void OccupyingAFullRow_IsRefused()
        {
            var row = new SlotRow(slots: 2);
            row.Occupy(WithAmmo(5));
            row.Occupy(WithAmmo(5));

            Assert.IsTrue(row.IsFull, "Two shooters in two slots did not fill the row.");
            Assert.Throws<InvalidOperationException>(() => row.Occupy(WithAmmo(5)),
                "A full row accepted one more shooter.");
        }

        /// <summary>
        /// Spending from an empty slot is refused. Without the check the empty marker's ammo
        /// goes negative and the slot springs back to life as occupied.
        /// </summary>
        [Test]
        public void SpendingFromAnEmptySlot_IsRefused()
        {
            var row = new SlotRow(slots: 2);

            Assert.Throws<InvalidOperationException>(() => row.Spend(0),
                "An empty slot handed out a shot.");
        }

        #endregion
    }
}
