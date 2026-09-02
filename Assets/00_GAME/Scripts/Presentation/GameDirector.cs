// GameDirector - the async glue: input in, GameLoop calls out, animations around both.
// Layer: Presentation.
// Responsibility: turning a tap into a TrySelect, pacing each seated shooter's TryShoot
//   calls, and dressing every result - the run to the slot, the queue step-up, the bullet
//   flight, the cube death and flow, the drained shooter's exit.
// NOT its responsibility: a single game rule. Every decision is a GameLoop call; if this
//   file ever contains an if about colours, ammo or verdicts beyond relaying them, that
//   logic has leaked out of the testable layer.
//
// Input is LeanTouch's own tap-to-select chain, wired in the scene inspector exactly as
// its "15 Tap To Select" example does: LeanFingerTap -> LeanSelectByFinger (raycast) ->
// this file's OnShooterSelected. No event subscription and no raycast live here; the
// ShooterView prefab carries a LeanSelectableByFinger so the query can find it.
//
// Why each fire loop counts its own ammo instead of watching the slot: the slot frees on
// the last shot and the PLAYER may seat a new shooter into it before this loop's next
// tick. A loop keyed on occupancy would then keep firing with the new tenant's ammo and
// the old tenant's view. The local count makes each loop fire exactly its own shots.
//
// Why the visual kill POPS the cube view before the bullet flies: the registry must hand
// out views in the exact order the domain removed cubes. Popping at impact time instead
// would let two in-flight shots at one column swap their victims.

using System;
using Blast.Application;
using Blast.Domain;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lean.Common;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Drives the play session: taps, fire rhythms and the visuals around them.</summary>
    public sealed class GameDirector : MonoBehaviour
    {
        #region Fields

        /// <summary>The bullet and splash pools. Assigned in the inspector; owns its own prewarm.</summary>
        [SerializeField] ShotPools _pools;

        /// <summary>How a shooter moves: to its slot, in place, off-screen.</summary>
        [SerializeField] ShooterMotion _motion = ShooterMotion.Defaults;

        /// <summary>The rhythm and geometry of a shot.</summary>
        [SerializeField] Firing _firing = Firing.Defaults;

        /// <summary>What happens to the board when a cube is hit.</summary>
        [SerializeField] CubeDeath _cubeDeath = CubeDeath.Defaults;

        /// <summary>The use case every action goes through. Handed in by Construct.</summary>
        GameLoop _loop;

        /// <summary>The slot row, read for positions of truth like remaining ammo.</summary>
        SlotRow _slots;

        /// <summary>The view registry: who stands where. Handed in by Construct.</summary>
        LevelSpawner _spawner;

        /// <summary>The colour table bullets dress from. Handed in by Construct.</summary>
        IColorMaterials _materials;

        /// <summary>Whether the verdict has been announced; it only happens once.</summary>
        bool _verdictAnnounced;

        #endregion

        #region Public Methods

        /// <summary>Receives the loop, the slots and the registry this director drives.</summary>
        /// <param name="loop">The use case every action goes through.</param>
        /// <param name="slots">The slot row, for ammo reads.</param>
        /// <param name="spawner">The view registry.</param>
        /// <param name="materials">The colour table bullets dress from.</param>
        public void Construct(GameLoop loop, SlotRow slots, LevelSpawner spawner, IColorMaterials materials)
        {
            _loop = loop;
            _slots = slots;
            _spawner = spawner;
            _materials = materials;
        }

        /// <summary>Turns a LeanSelectByFinger selection of a front-row shooter into a play.</summary>
        /// <param name="selectable">The selectable LeanTouch's raycast landed on.</param>
        public void OnShooterSelected(LeanSelectable selectable)
        {
            // Construct runs from the scope's Configure; a tap can arrive before it.
            if (_loop == null)
            {
                return;
            }

            ShooterView tapped = selectable.GetComponent<ShooterView>();
            if (tapped == null || !_spawner.TryGetSelectableColumn(tapped, out int column))
            {
                return;
            }

            OnSelected(column).Forget();
        }

        #endregion

        #region Private Methods

        /// <summary>Runs one selection: seat in the domain, then animate the consequences.</summary>
        /// <param name="column">The queue column the player tapped.</param>
        async UniTaskVoid OnSelected(int column)
        {
            if (!_loop.TrySelect(column, out int slot))
            {
                return;
            }

            // Domain first, views second: these reads must see the seated shooter, and
            // the slot may free long before the bullet material is last needed.
            int ammo = _slots.AmmoAt(slot);
            Material bulletMaterial = _materials.MaterialOf(_slots.ShooterAt(slot).Color);

            ShooterView view = _spawner.PopFrontShooter(column);
            _spawner.StepQueueForward(column, _motion.StepDuration);

            AnnounceIfDecided();

            Vector3 slotPosition = _spawner.SlotWorldPosition(slot);

            view.SetRunning(true);
            view.TurnTo(slotPosition, _motion.TurnDuration);
            await view.transform.DOMove(slotPosition, _motion.RunDuration).SetEase(Ease.OutQuad);
            view.SetRunning(false);
            view.FaceForward(_motion.TurnDuration);

            FireLoop(slot, view, ammo, bulletMaterial).Forget();
        }

        /// <summary>One seated shooter's whole life: fire, wait when targetless, leave when dry.</summary>
        /// <param name="slot">The slot the shooter fires from.</param>
        /// <param name="view">The shooter's visual.</param>
        /// <param name="ammo">Its own shots - see the header for why the slot is not read.</param>
        /// <param name="bulletMaterial">What this shooter's bullets wear.</param>
        async UniTaskVoid FireLoop(int slot, ShooterView view, int ammo, Material bulletMaterial)
        {
            while (ammo > 0)
            {
                if (_loop.Verdict != GameVerdict.Playing)
                {
                    return;
                }

                if (_loop.TryShoot(slot, out int hitColumn))
                {
                    ammo--;
                    view.SetAmmo(ammo);
                    view.PlayShoot();
                    ShotVisual(view, bulletMaterial, hitColumn).Forget();
                    AnnounceIfDecided();
                }
                else
                {
                    // Targetless: stand idle, facing the board, until a matching cube reaches the front.
                    view.SetRunning(false);
                    view.FaceForward(_motion.TurnDuration);
                }

                // One rhythm for firing and for waiting: a targetless shooter re-checks at
                // the same rate it would have fired, which reads naturally on screen.
                await UniTask.Delay(TimeSpan.FromSeconds(_firing.Interval));
            }

            await Leave(view);
        }

        /// <summary>One shot on screen: the bullet flies, the cube dies, the column flows.</summary>
        /// <param name="shooter">The visual that fired.</param>
        /// <param name="bulletMaterial">What the bullet wears.</param>
        /// <param name="hitColumn">The board column the domain says was hit.</param>
        async UniTaskVoid ShotVisual(ShooterView shooter, Material bulletMaterial, int hitColumn)
        {
            // Popped now, not at impact - the registry must hand views out in domain
            // removal order, or two in-flight shots at one column swap their victims.
            CubeView cube = _spawner.PopFrontCube(hitColumn);

            // Held until the survivors have landed: the domain already sees the next cube
            // as the front, but the player must not watch a shot land on a cube that is
            // still sliding into place. Synchronous, before the first await, so no other
            // shooter's TryShoot in this frame can pick the column either.
            _loop.HoldColumn(hitColumn);

            shooter.TurnTo(cube.transform.position, _motion.TurnDuration);

            Vector3 muzzle = shooter.transform.position + Vector3.up * _firing.MuzzleHeight;

            // The shooter is still mid-turn when the shot leaves, so the splash takes the
            // aim itself: yaw toward the cube, the same axis TurnTo constrains the body to.
            Vector3 aim = Vector3.ProjectOnPlane(cube.transform.position - muzzle, Vector3.up);
            _pools.Splashes.Take(muzzle, Quaternion.LookRotation(aim));

            CubeView bullet = _pools.Bullets.Take(muzzle);
            bullet.Wear(bulletMaterial);

            await bullet.transform.DOMove(cube.transform.position, _firing.FlightDuration).SetEase(Ease.Linear);

            _pools.Bullets.Return(bullet);

            // Death first, flow second - the original's order. The survivors only start
            // sliding once the dead cube is gone, so the eye reads two beats, not one blur.
            // InBack swells before it collapses; at DOTween's default overshoot (1.7) the
            // swell peaks at +10% and is invisible, so the overshoot is a tuned field.
            await cube.transform.DOScale(Vector3.zero, _cubeDeath.ShrinkDuration)
                .SetEase(Ease.InBack, _cubeDeath.ShrinkOvershoot);

            Destroy(cube.gameObject);

            Tween slide = _spawner.FlowBoardColumn(
                hitColumn, _cubeDeath.FlowDuration, _cubeDeath.SettleDistance, _cubeDeath.SettleDuration);
            if (slide != null)
            {
                await slide;
            }

            _loop.ReleaseColumn(hitColumn);
        }

        /// <summary>Runs a drained shooter off-screen and despawns it.</summary>
        /// <param name="view">The shooter's visual.</param>
        async UniTask Leave(ShooterView view)
        {
            Vector3 offScreen = view.transform.position + new Vector3(0f, 0f, -_motion.LeaveDistance);

            view.SetRunning(true);
            view.TurnTo(offScreen, _motion.TurnDuration);
            await view.transform.DOMove(offScreen, _motion.LeaveDuration).SetEase(Ease.InQuad);

            Destroy(view.gameObject);
        }

        /// <summary>Announces the verdict once. The overlay UI replaces this in its own chunk.</summary>
        void AnnounceIfDecided()
        {
            if (_verdictAnnounced || _loop.Verdict == GameVerdict.Playing)
            {
                return;
            }

            _verdictAnnounced = true;
            Debug.Log($"Level over: {_loop.Verdict}");
        }

        #endregion

        #region Nested Types

        /// <summary>How a shooter moves: to its slot, in place, off-screen. One inspector heading.</summary>
        [Serializable]
        public struct ShooterMotion
        {
            /// <summary>How long a selected shooter runs to its slot.</summary>
            public float RunDuration;

            /// <summary>How long a shooter takes to turn: toward where it runs, toward what it shoots, back to forward.</summary>
            public float TurnDuration;

            /// <summary>How long the queue's step-up takes after a selection.</summary>
            public float StepDuration;

            /// <summary>How long a drained shooter takes to run off-screen.</summary>
            public float LeaveDuration;

            /// <summary>How far off-screen a drained shooter runs before despawning.</summary>
            public float LeaveDistance;

            /// <summary>The values a fresh director starts with.</summary>
            public static ShooterMotion Defaults => new ShooterMotion
            {
                RunDuration = 0.45f,
                TurnDuration = 0.15f,
                StepDuration = 0.25f,
                LeaveDuration = 0.6f,
                LeaveDistance = 6f,
            };
        }

        /// <summary>The rhythm and geometry of a shot. One inspector heading.</summary>
        [Serializable]
        public struct Firing
        {
            /// <summary>Seconds between a seated shooter's shots - also its idle re-check rate.</summary>
            public float Interval;

            /// <summary>Height above a shooter's feet a bullet leaves from.</summary>
            public float MuzzleHeight;

            /// <summary>How long a bullet flies to its cube.</summary>
            public float FlightDuration;

            /// <summary>The values a fresh director starts with.</summary>
            public static Firing Defaults => new Firing { Interval = 0.22f, MuzzleHeight = 0.7f, FlightDuration = 0.12f };
        }

        /// <summary>What happens to the board when a cube is hit. One inspector heading.</summary>
        [Serializable]
        public struct CubeDeath
        {
            /// <summary>How long a dying cube shrinks away.</summary>
            public float ShrinkDuration;

            /// <summary>InBack's overshoot: how much the cube swells before collapsing (3 = +25% at mid-tween).</summary>
            public float ShrinkOvershoot;

            /// <summary>How long a column's survivors take to flow one cell forward.</summary>
            public float FlowDuration;

            /// <summary>How far a flowed cube overshoots its cell before bouncing back into it.</summary>
            public float SettleDistance;

            /// <summary>How long that landing bounce takes.</summary>
            public float SettleDuration;

            /// <summary>The values a fresh director starts with - the clone's numbers, which read right.</summary>
            public static CubeDeath Defaults => new CubeDeath
            {
                ShrinkDuration = 0.15f,
                ShrinkOvershoot = 3f,
                FlowDuration = 0.15f,
                SettleDistance = 0.1f,
                SettleDuration = 0.15f,
            };
        }

        #endregion
    }
}
