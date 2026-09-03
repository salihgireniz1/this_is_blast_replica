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

        /// <summary>The impact shake on the camera. Assigned in the inspector; owns its own tween.</summary>
        [Tooltip("The CameraShake on the main camera.")]
        [SerializeField] CameraShake _shake;

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

            // When a row falls, the column walks three states. Locked while the bullet
            // flies and the cube takes the hit: the domain already sees the next cube as
            // the front, and without the lock a second shooter fires at it before this
            // bullet has landed. Settling from the collapse to the end of the slide: other
            // matching fronts are shot first and the shooter comes back once the cube
            // stands still, unless it is the only match, in which case it fires at once,
            // as the original does. While the stack still stands, the next cube is right
            // under the dying one, so no states. Synchronous, before the first await, so
            // no other shooter's TryShoot in this frame can pick this column either.
            bool rowFalls = !_spawner.StackStillStands(hitColumn);
            if (rowFalls)
            {
                _loop.LockColumn(hitColumn);
            }

            shooter.TurnTo(cube.transform.position, _motion.TurnDuration);

            Vector3 muzzle = shooter.transform.position + Vector3.up * _firing.MuzzleHeight;

            // The shooter is still mid-turn when the shot leaves, so the splash takes the
            // aim itself: yaw toward the cube, the same axis TurnTo constrains the body to.
            Vector3 aim = Vector3.ProjectOnPlane(cube.transform.position - muzzle, Vector3.up);
            _pools.Splashes.Take(muzzle, Quaternion.LookRotation(aim));
            _audio.Play();

            // The bullet keeps the prefab's own material (one bullet colour for every shooter)
            // and takes the same aim as the splash: its head is +Z and the streak sprite
            // trails behind it, so a bullet spawned in the prefab's rotation would fly sideways.
            CubeView bullet = _pools.Bullets.Take(muzzle, Quaternion.LookRotation(aim));

            await bullet.transform.DOMove(cube.transform.position, _firing.FlightDuration)
                .SetEase(Ease.Linear)
                .ToUniTask(cancellationToken: _destroyed);

            _pools.Bullets.Return(bullet);

            // The impact splash sits on the cube, the muzzle one on the shooter: the
            // original shows both, and the pool does not care where a splash plays.
            _pools.Splashes.Take(cube.transform.position, Quaternion.LookRotation(aim));
            _shake.Kick();

            // Death as the original plays it, measured frame by frame (see CLAUDE.md): the
            // cube shrinks to nothing in place, fast at first and slow at the end. No rock,
            // no jelly, no hop (the original's one-frame lift was tried and could not be
            // seen). Awaited because the column only flows once the cube is gone.
            Transform dying = cube.transform;
            await dying.DOScale(Vector3.zero, _cubeDeath.CollapseDuration)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: _destroyed);

            // The hit has played out; from here on the column is a last resort.
            if (rowFalls)
            {
                _loop.MarkSettling(hitColumn);
            }

            Destroy(cube.gameObject);

            // Death first, flow second - the original's order. The survivors only start
            // sliding once the dead cube is gone, so the eye reads two beats, not one blur.
            if (!rowFalls)
            {
                return;
            }

            Tween slide = _spawner.FlowBoardColumn(hitColumn, _cubeDeath.FlowDuration, _cubeDeath.SettleOvershoot);
            if (slide != null)
            {
                await slide.ToUniTask(cancellationToken: _destroyed);
            }

            _loop.MarkSettled(hitColumn);
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
            public static Firing Defaults => new Firing { Interval = 0.12f, MuzzleHeight = 0.5f, FlightDuration = 0.17f };
        }

        /// <summary>What happens to the board when a cube is hit. One inspector heading.</summary>
        [Serializable]
        public struct CubeDeath
        {
            /// <summary>How long the shrink to nothing takes, from impact. Eased out: fast at first, slow at the end.</summary>
            [Tooltip("Seconds from impact until the cube has shrunk to nothing. OutQuad: most of the shrink happens in the first half.")]
            public float CollapseDuration;

            /// <summary>How long a column's survivors take to flow one cell forward, overshoot and settle included.</summary>
            [Tooltip("Seconds a column's survivors take to slide one cell forward and settle, after the shrink has finished. OutBack: the rest is crossed at ~40%, the overshoot peaks at ~60%.")]
            public float FlowDuration;

            /// <summary>How far the slide overshoots its cell before easing back, as DOTween's OutBack overshoot.</summary>
            [Tooltip("OutBack overshoot. 1.7 (DOTween's default) peaks ~10% of a cell past the rest, the original's bump; 0 is a plain ease-out.")]
            public float SettleOvershoot;

            /// <summary>The values a fresh director starts with - the original's, measured from a frame-by-frame capture.</summary>
            public static CubeDeath Defaults => new CubeDeath
            {
                CollapseDuration = 0.18f,
                FlowDuration = 0.3f,
                SettleOvershoot = 1.7f,
            };
        }

        #endregion
    }
}
