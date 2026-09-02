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
using System.Threading;
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

        /// <summary>How far ahead of itself a shooter looks while following its leave path; near zero keeps the nose on the curve.</summary>
        const float PathLookAhead = 0.01f;

        /// <summary>How many points the leave curve is sampled into; enough for a Catmull-Rom to follow a single bend.</summary>
        const int LeavePathSamples = 8;

        /// <summary>The bullet and splash pools. Assigned in the inspector; owns its own prewarm.</summary>
        [Tooltip("The scene's ShotPools object.")]
        [SerializeField] ShotPools _pools;

        /// <summary>The shot sound. Assigned in the inspector; owns its own voice cap.</summary>
        [Tooltip("The scene's ShotAudio object.")]
        [SerializeField] ShotAudio _audio;

        /// <summary>How a shooter moves: to its slot, in place, off-screen.</summary>
        [Tooltip("How shooters move: to the slot, turning, stepping up, leaving.")]
        [SerializeField] ShooterMotion _motion = ShooterMotion.Defaults;

        /// <summary>The rhythm and geometry of a shot.</summary>
        [Tooltip("Shot rhythm and where bullets spawn.")]
        [SerializeField] Firing _firing = Firing.Defaults;

        /// <summary>What happens to the board when a cube is hit.</summary>
        [Tooltip("What a hit cube and its column do.")]
        [SerializeField] CubeDeath _cubeDeath = CubeDeath.Defaults;

        /// <summary>The use case every action goes through. Handed in by Construct.</summary>
        GameLoop _loop;

        /// <summary>The slot row, read for positions of truth like remaining ammo.</summary>
        SlotRow _slots;

        /// <summary>The view registry: who stands where. Handed in by Construct.</summary>
        LevelSpawner _spawner;

        /// <summary>Cancelled when this director is destroyed - a restart reloads the scene
        /// while shots are still in flight, and every await below stops here instead of
        /// waking up to touch a destroyed view.</summary>
        CancellationToken _destroyed;

        #endregion

        #region Public Methods

        /// <summary>Takes the token every async flow below is tied to.</summary>
        void Awake()
        {
            _destroyed = this.GetCancellationTokenOnDestroy();
        }

        /// <summary>Receives the loop, the slots and the registry this director drives.</summary>
        /// <param name="loop">The use case every action goes through.</param>
        /// <param name="slots">The slot row, for ammo reads.</param>
        /// <param name="spawner">The view registry.</param>
        public void Construct(GameLoop loop, SlotRow slots, LevelSpawner spawner)
        {
            _loop = loop;
            _slots = slots;
            _spawner = spawner;
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

            // Domain first, views second: this read must see the seated shooter.
            int ammo = _slots.AmmoAt(slot);

            ShooterView view = _spawner.PopFrontShooter(column);
            _spawner.StepQueueForward(column, _motion.StepDuration);

            Vector3 slotPosition = _spawner.SlotWorldPosition(slot);

            view.SetRunning(true);
            view.TurnTo(slotPosition, _motion.TurnDuration);
            await view.transform.DOMove(slotPosition, _motion.RunDuration)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: _destroyed);
            view.SetRunning(false);
            view.FaceForward(_motion.TurnDuration);

            // The thump on arrival. Not awaited: the first shot may leave mid-squash, which
            // is how the original reads too. vibrato is per second: 10 x 0.3 s = 3 segments
            // of 0.1 s - the squash itself is the first one, and it has to outlast the frame
            // hitch the first shot causes on this same frame (measured 56 ms in the editor:
            // a 0.067 s segment vanished inside it). Low elasticity keeps the swing back
            // through the opposite stretch small, so what reads is the squash.
            view.transform.DOPunchScale(_motion.LandSquash, _motion.LandDuration, vibrato: 10, elasticity: 0.3f);

            FireLoop(slot, view, ammo).Forget();
        }

        /// <summary>One seated shooter's whole life: fire, wait when targetless, leave when dry.</summary>
        /// <param name="slot">The slot the shooter fires from.</param>
        /// <param name="view">The shooter's visual.</param>
        /// <param name="ammo">Its own shots - see the header for why the slot is not read.</param>
        async UniTaskVoid FireLoop(int slot, ShooterView view, int ammo)
        {
            while (ammo > 0)
            {
                if (_loop.Verdict != GameVerdict.Playing)
                {
                    // Frozen, not abandoned: PlayShoot dropped isIdle and only this tick
                    // would have raised it again, so without this the last shooter to
                    // fire stays in its Shoot pose under the overlay.
                    view.SetRunning(false);
                    view.FaceForward(_motion.TurnDuration);
                    return;
                }

                if (_loop.TryShoot(slot, out int hitColumn))
                {
                    ammo--;
                    view.SetAmmo(ammo);
                    view.PlayShoot();
                    ShotVisual(view, hitColumn).Forget();
                }
                else
                {
                    // Targetless: stand idle, facing the board, until a matching cube reaches the front.
                    view.SetRunning(false);
                    view.FaceForward(_motion.TurnDuration);
                }

                // One rhythm for firing and for waiting: a targetless shooter re-checks at
                // the same rate it would have fired, which reads naturally on screen.
                await UniTask.Delay(TimeSpan.FromSeconds(_firing.Interval), cancellationToken: _destroyed);
            }

            await Leave(view);
        }

        /// <summary>One shot on screen: the bullet flies, the cube dies, the column flows.</summary>
        /// <param name="shooter">The visual that fired.</param>
        /// <param name="hitColumn">The board column the domain says was hit.</param>
        async UniTaskVoid ShotVisual(ShooterView shooter, int hitColumn)
        {
            // Popped now, not at impact - the registry must hand views out in domain
            // removal order, or two in-flight shots at one column swap their victims.
            CubeView cube = _spawner.PopFrontCube(hitColumn);

            // Held until the survivors have landed, but only when a row actually falls: the
            // domain already sees the next cube as the front, and the player must not watch
            // a shot land on a cube that is still sliding into place. While the stack still
            // stands, the next cube is right there under the dying one, so no hold - and
            // because the hold is what made TryFindTarget skip this column, skipping it here
            // is what keeps a shooter on one stack until it is gone instead of hopping to
            // the next matching column mid-stack (the original finishes a stack top-down).
            // Synchronous, before the first await, so no other shooter's TryShoot in this
            // frame can pick a falling column either.
            bool rowFalls = !_spawner.StackStillStands(hitColumn);
            if (rowFalls)
            {
                _loop.HoldColumn(hitColumn);
            }

            shooter.TurnTo(cube.transform.position, _motion.TurnDuration);

            Vector3 muzzle = shooter.transform.position + Vector3.up * _firing.MuzzleHeight;

            // The shooter is still mid-turn when the shot leaves, so the splash takes the
            // aim itself: yaw toward the cube, the same axis TurnTo constrains the body to.
            Vector3 aim = Vector3.ProjectOnPlane(cube.transform.position - muzzle, Vector3.up);
            _pools.Splashes.Take(muzzle, Quaternion.LookRotation(aim));
            _audio.Play();

            // The bullet keeps the prefab's own material: one bullet colour for every shooter.
            CubeView bullet = _pools.Bullets.Take(muzzle);

            await bullet.transform.DOMove(cube.transform.position, _firing.FlightDuration)
                .SetEase(Ease.Linear)
                .ToUniTask(cancellationToken: _destroyed);

            _pools.Bullets.Return(bullet);

            // Death in three beats. The rock and the swell start together on impact; the
            // rock is fire-and-forget because nothing waits for it, the swell is awaited
            // because the collapse starts the moment it ends. Rotation and scale are
            // different properties, so the rock keeps wobbling through the collapse.
            Transform dying = cube.transform;
            // vibrato is per second: 15 x 0.3 s = 4 segments, so the rock swings back twice.
            dying.DOPunchRotation(Vector3.up * _cubeDeath.RockAngle, _cubeDeath.RockDuration, vibrato: 15, elasticity: 1f)
                .ToUniTask()
                .Forget();
            await dying.DOPunchScale(_cubeDeath.SwellScale, _cubeDeath.SwellDuration, _cubeDeath.SwellVibrato, _cubeDeath.SwellElasticity)
                .ToUniTask(cancellationToken: _destroyed);
            await dying.DOScale(Vector3.zero, _cubeDeath.CollapseDuration)
                .SetEase(Ease.InQuad)
                .ToUniTask(cancellationToken: _destroyed);

            // The rock may still be running; kill it explicitly rather than leaning on
            // safe mode to notice the target is gone.
            dying.DOKill();
            Destroy(cube.gameObject);

            // Death first, flow second - the original's order. The survivors only start
            // sliding once the dead cube is gone, so the eye reads two beats, not one blur.

            if (!rowFalls)
            {
                return;
            }

            Tween slide = _spawner.FlowBoardColumn(
                hitColumn, _cubeDeath.FlowDuration, _cubeDeath.SettleAngle, _cubeDeath.SettleDuration);
            if (slide != null)
            {
                await slide.ToUniTask(cancellationToken: _destroyed);
            }

            _loop.ReleaseColumn(hitColumn);
        }

        /// <summary>Runs a drained shooter off the nearer side of the screen and despawns it.</summary>
        /// <param name="view">The shooter's visual.</param>
        async UniTask Leave(ShooterView view)
        {
            // Toward whichever edge is closer: the original's shooters never back out
            // through the queue. Mathf.Sign(0) is +1, so the centre slot goes right.
            float side = Mathf.Sign(view.transform.position.x);
            Vector3 start = view.transform.position;

            // The inspector curve draws the run's shape: x is how far along the sideways
            // distance, y is how far forward at that point. Sampling it into a Catmull-Rom
            // path keeps the tween machinery, and the look-at replaces TurnTo because the
            // heading changes all along the curve, not once at the start.
            Vector3[] path = new Vector3[LeavePathSamples];
            for (int i = 0; i < LeavePathSamples; i++)
            {
                float progress = (i + 1f) / LeavePathSamples;
                float forward = _motion.LeavePath.Evaluate(progress);
                path[i] = start + new Vector3(side * _motion.LeaveDistance * progress, 0f, forward);
            }

            view.SetRunning(true);
            await view.transform.DOPath(path, _motion.LeaveDuration, PathType.CatmullRom)
                .SetEase(Ease.InQuad)
                .SetLookAt(PathLookAhead)
                .ToUniTask(cancellationToken: _destroyed);

            Destroy(view.gameObject);
        }

        #endregion

        #region Nested Types

        /// <summary>How a shooter moves: to its slot, in place, off-screen. One inspector heading.</summary>
        [Serializable]
        public struct ShooterMotion
        {
            /// <summary>How long a selected shooter runs to its slot.</summary>
            [Tooltip("Seconds a tapped shooter takes to run from the queue to its slot.")]
            public float RunDuration;

            /// <summary>How long a shooter takes to turn: toward where it runs, toward what it shoots, back to forward.</summary>
            [Tooltip("Seconds for any turn: toward the slot, toward the aimed cube, back to forward.")]
            public float TurnDuration;

            /// <summary>How long the queue's step-up takes after a selection.</summary>
            [Tooltip("Seconds the rest of a queue column takes to step up after a selection.")]
            public float StepDuration;

            /// <summary>How long a drained shooter takes to run off-screen.</summary>
            [Tooltip("Seconds a drained shooter takes to run off-screen.")]
            public float LeaveDuration;

            /// <summary>How far sideways a drained shooter runs before despawning; must clear the portrait frame's half-width.</summary>
            [Tooltip("World units a drained shooter runs sideways. Must exceed the portrait frame's half-width (about 6.5 at ortho size 11.5) or it stops on screen.")]
            public float LeaveDistance;

            /// <summary>The leave run's shape: x is the fraction of LeaveDistance covered sideways, y is the forward offset in world units at that point.</summary>
            [Tooltip("Shape of the leave run. X: fraction of Leave Distance covered sideways. Y: world units forward of the slot at that point.")]
            public AnimationCurve LeavePath;

            /// <summary>The squash a shooter lands with: per axis, as a fraction of its scale. Negative y and positive x/z read as a thump.</summary>
            [Tooltip("The landing squash on arrival at the slot, per axis as a fraction of scale. Wider and flatter (negative y) reads as a thump; the punch swings back through the opposite before settling.")]
            public Vector3 LandSquash;

            /// <summary>How long the landing squash takes to settle.</summary>
            [Tooltip("Seconds the landing squash takes to settle.")]
            public float LandDuration;

            /// <summary>The values a fresh director starts with.</summary>
            public static ShooterMotion Defaults => new ShooterMotion
            {
                RunDuration = 0.45f,
                TurnDuration = 0.15f,
                StepDuration = 0.25f,
                LeaveDuration = 0.6f,
                LeaveDistance = 10f,
                LeavePath = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 2f), new Keyframe(1f, 2f)),
                LandSquash = new Vector3(0.2f, -0.25f, 0.2f),
                LandDuration = 0.3f,
            };
        }

        /// <summary>The rhythm and geometry of a shot. One inspector heading.</summary>
        [Serializable]
        public struct Firing
        {
            /// <summary>Seconds between a seated shooter's shots - also its idle re-check rate.</summary>
            [Tooltip("Seconds between one seated shooter's shots, and how often a targetless shooter re-checks the board.")]
            public float Interval;

            /// <summary>Height above a shooter's feet a bullet leaves from.</summary>
            [Tooltip("World units above the shooter's feet where the bullet and splash spawn.")]
            public float MuzzleHeight;

            /// <summary>How long a bullet flies to its cube.</summary>
            [Tooltip("Seconds a bullet takes to reach its cube. The cube dies on arrival.")]
            public float FlightDuration;

            /// <summary>The values a fresh director starts with.</summary>
            public static Firing Defaults => new Firing { Interval = 0.22f, MuzzleHeight = 0.7f, FlightDuration = 0.12f };
        }

        /// <summary>What happens to the board when a cube is hit. One inspector heading.</summary>
        [Serializable]
        public struct CubeDeath
        {
            /// <summary>Degrees a hit cube rocks forward as it dies; a jelly wobble that runs through the collapse.</summary>
            [Tooltip("Degrees a hit cube rocks forward on impact. The wobble runs alongside the swell and the collapse.")]
            public float RockAngle;

            /// <summary>How long the rock keeps wobbling.</summary>
            [Tooltip("Seconds the impact rock keeps wobbling.")]
            public float RockDuration;

            /// <summary>How much a hit cube swells on impact, per axis, as a fraction of its size.
            /// Opposite signs on x/z against y give squash-and-stretch; uniform reads as a breath.</summary>
            [Tooltip("Impact swell per axis, as a fraction of size. (0.3, -0.25, 0.3) = wider and flatter, then the reverse: jelly. Uniform = a breath.")]
            public Vector3 SwellScale;

            /// <summary>How long the swell wobbles before the collapse starts.</summary>
            [Tooltip("Seconds the swell wobbles. The collapse starts the moment it ends. Under ~0.25 the wobble has no frames to show in.")]
            public float SwellDuration;

            /// <summary>Oscillations per second. DOTween cuts the punch into vibrato x duration
            /// segments (rounded down): 2 segments is out-and-back with no bounce at all, the
            /// first opposite swing needs 3, a visible jelly needs 6 or more.</summary>
            [Tooltip("Oscillations per SECOND, not per punch. Segments = vibrato x duration, rounded down: 2 = out and back, no bounce; 3 = one opposite swing; 6+ = jelly. 20 x 0.35 s = 7.")]
            public int SwellVibrato;

            /// <summary>How far the swell overshoots the other way: 0 = swell and settle, 1 = shrink as far as it swelled.</summary>
            [Tooltip("0 = swells then settles; 1 = shrinks as far as it swelled on the way back. Jelly wants ~1.")]
            [Range(0f, 1f)]
            public float SwellElasticity;

            /// <summary>How long the collapse to nothing takes, after the swell.</summary>
            [Tooltip("Seconds the cube takes to collapse to nothing once the swell has ended.")]
            public float CollapseDuration;

            /// <summary>How long a column's survivors take to flow one cell forward.</summary>
            [Tooltip("Seconds a column's survivors take to slide one cell forward, after the shrink has finished.")]
            public float FlowDuration;

            /// <summary>How far a flowed cube tips forward on landing before rocking back upright.</summary>
            [Tooltip("Degrees a flowed cube tips forward on landing before rocking back upright.")]
            public float SettleAngle;

            /// <summary>How long that landing bounce takes.</summary>
            [Tooltip("Seconds that landing bounce takes.")]
            public float SettleDuration;

            /// <summary>The values a fresh director starts with - the clone's numbers, which read right.</summary>
            public static CubeDeath Defaults => new CubeDeath
            {
                RockAngle = 20f,
                RockDuration = 0.3f,
                SwellScale = new Vector3(0.3f, -0.25f, 0.3f),
                SwellDuration = 0.35f,
                SwellVibrato = 20,
                SwellElasticity = 1f,
                CollapseDuration = 0.12f,
                FlowDuration = 0.15f,
                SettleAngle = 15f,
                SettleDuration = 0.15f,
            };
        }

        #endregion
    }
}
