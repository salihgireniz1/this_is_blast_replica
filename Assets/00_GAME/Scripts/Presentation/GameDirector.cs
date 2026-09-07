// GameDirector - the async glue: input in, GameLoop calls out, animations around both.
// Layer: Presentation.
// Responsibility: turning a tap into a TrySelect, pacing each seated shooter's TryShoot
//   calls, and dressing every result - the run to the slot, the queue step-up, the bullet
//   flight and the cubes it brushes on the way, the cube death and flow, the drained
//   shooter's exit.
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
using VContainer;

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

        /// <summary>What a cube does when a bullet brushes past it on the way to another.</summary>
        [Tooltip("How a cube leans when a bullet passes through it without hitting it.")]
        [SerializeField] Brush _brush = Brush.Defaults;

        /// <summary>The use case every action goes through. Handed in by Construct.</summary>
        GameLoop _loop;

        /// <summary>The slot row, read for positions of truth like remaining ammo.</summary>
        SlotRow _slots;

        /// <summary>The view registry: who stands where. Handed in by Construct.</summary>
        LevelSpawner _spawner;

        /// <summary>The colour table, read once per seated shooter for what its shots wear. Handed in by Construct.</summary>
        IColorMaterials _materials;

        /// <summary>Cancelled when this director is destroyed - a restart reloads the scene
        /// while shots are still in flight, and every await below stops here instead of
        /// waking up to touch a destroyed view.</summary>
        CancellationToken _destroyed;

        /// <summary>The shared collapse tweens every cube death shrinks through; a DOScale per death was two closures per shot.</summary>
        readonly CollapseTweens _collapses = new CollapseTweens();

        #endregion

        #region Public Methods

        /// <summary>Receives the loop, the slots and the registry this director drives.</summary>
        /// <param name="loop">The use case every action goes through.</param>
        /// <param name="slots">The slot row, for ammo reads.</param>
        /// <param name="spawner">The view registry.</param>
        /// <param name="materials">The colour table a shot's bullet and splashes are dressed from.</param>
        [Inject]
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

        /// <summary>Takes the token every async flow below is tied to.</summary>
        void Awake()
        {
            _destroyed = this.GetCancellationTokenOnDestroy();
        }

        /// <summary>Kills the reused collapse tweens with the director: they never auto-kill, and a reload builds a new set.</summary>
        void OnDestroy()
        {
            _collapses.KillAll();
        }

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
            view.transform.DOPunchScale(_motion.LandSquash, _motion.LandDuration, vibrato: 10, elasticity: 0.3f).ToUniTask().Forget();

            FireLoop(slot, view, ammo).Forget();
        }

        /// <summary>One seated shooter's whole life: fire, wait when targetless, leave when dry.</summary>
        /// <param name="slot">The slot the shooter fires from.</param>
        /// <param name="view">The shooter's visual.</param>
        /// <param name="ammo">Its own shots - see the header for why the slot is not read.</param>
        async UniTaskVoid FireLoop(int slot, ShooterView view, int ammo)
        {
            // Resolved once per seating, not per shot: the slot holds this shooter until its
            // last shot, and a hidden shooter is revealed by the time it can be selected.
            BlastColor color = _slots.ShooterAt(slot).Color;
            Material bulletMaterial = _materials.MaterialOf(color);
            Color tint = _materials.TintOf(color);

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
                    ShotVisual(view, hitColumn, bulletMaterial, tint).Forget();
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
        /// <param name="bulletMaterial">What the bullet wears: the shooter's colour material.</param>
        /// <param name="tint">The flat tint of the shooter's colour, for the splashes and the trail.</param>
        async UniTaskVoid ShotVisual(ShooterView shooter, int hitColumn, Material bulletMaterial, Color tint)
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

            Vector3 chest = shooter.transform.position + Vector3.up * _firing.MuzzleHeight;

            // The shooter is still mid-turn when the shot leaves, so the splash takes the
            // aim itself: yaw toward the cube, the same axis TurnTo constrains the body to.
            Vector3 aim = Vector3.ProjectOnPlane(cube.transform.position - chest, Vector3.up);

            // Out past the body, or the shot leaves from inside the shooter: MuzzleHeight
            // only lifts, and a shooter is a whole cube wide, so at zero reach the splash
            // and the bullet both spawn buried in the chest that fired them.
            Vector3 muzzle = chest + aim.normalized * _firing.MuzzleReach;

            _pools.Splashes.Take(muzzle, Quaternion.LookRotation(aim)).Tint(tint);
            _audio.Play();

            // The bullet is dressed per shot, since the pool hands the same five out to every
            // colour: sharedMaterial on the body, vertex colour on the trail, nothing
            // instanced. It takes the same aim as the splash: its head is +Z and the streak
            // sprite trails behind it, so a bullet spawned in the prefab's rotation would fly sideways.
            CubeView bullet = _pools.Bullets.Take(muzzle, Quaternion.LookRotation(aim));
            bullet.Wear(bulletMaterial);
            bullet.Tint(tint);

            // FlyTo re-targets the bullet's one tween: five pooled bullets fly forty times a
            // second between them, and a DOMove per flight was two closures each. A reused
            // tween never auto-kills, and ToUniTask waits for the kill: AwaitForComplete
            // waits for the landing, which is the event.
            Vector3 target = cube.transform.position;
            Tween flight = bullet.FlyTo(target, _firing.FlightDuration);

            // The bullet brushes every standing front cube its line crosses, the frame it
            // crosses the face: the original's cubes lean as a shot passes through them.
            // Polled per frame rather than awaited, because the brushes need the flight's
            // progress and an OnUpdate closure per shot would allocate; a bit per column
            // remembers what this bullet has already brushed.
            int brushed = 0;
            while (flight.IsActive() && !flight.IsComplete())
            {
                await UniTask.Yield(PlayerLoopTiming.Update, _destroyed);
                brushed = BrushAlongFlight(muzzle, target, aim, flight.ElapsedPercentage(), hitColumn, brushed);
            }

            _pools.Bullets.Return(bullet);

            // The impact splash sits on the cube, the muzzle one on the shooter: the
            // original shows both, and the pool does not care where a splash plays. Pulled
            // back along the flight line toward the face the bullet struck and lifted to the
            // top face: at the cube's centre the neighbours hide most of it and it reads as
            // a dim flicker between the cubes.
            Vector3 impact = cube.transform.position - aim.normalized * _firing.ImpactPullback + Vector3.up * _firing.ImpactLift;
            _pools.Splashes.Take(impact, Quaternion.LookRotation(aim)).Tint(tint);
            _shake.Kick();

            // Death in place, shaped by the designer's curve (scale over time; the default is
            // the original's shrink, measured frame by frame: fast at first, slow at the end).
            // Awaited because the column only flows once the cube is gone. The collapse
            // comes from the shared pool: a DOScale here was two closures per death, and a
            // cube dies once, so no per-cube tween could have saved them. A reused tween
            // never kills, so AwaitForComplete, not ToUniTask.
            await _collapses.Play(cube.transform, _cubeDeath.CollapseDuration, _cubeDeath.CollapseCurve)
                .AwaitForComplete(cancellationToken: _destroyed);

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
                // The slide is a reused tween too (CubeView.SlideTo): complete, not kill.
                await slide.AwaitForComplete(cancellationToken: _destroyed);
            }

            _loop.MarkSettled(hitColumn);
        }

        /// <summary>Nudges every standing front cube the flight has entered so far and not brushed yet.</summary>
        /// <param name="from">Where the flight started.</param>
        /// <param name="to">Where it ends: the target cube's centre.</param>
        /// <param name="aim">The flight's direction on the board plane; its x decides which way the cubes lean.</param>
        /// <param name="progress">How far along the flight the bullet is, 0..1.</param>
        /// <param name="hitColumn">The target's column; its front is the cube being shot, not brushed.</param>
        /// <param name="brushed">One bit per column, set once that column's front has been nudged by this bullet.</param>
        /// <returns>The mask with this frame's brushes added.</returns>
        int BrushAlongFlight(Vector3 from, Vector3 to, Vector3 aim, float progress, int hitColumn, int brushed)
        {
            // A rightward shot spins the cube clockwise seen from above, the way the original's
            // cubes lean into the bullet's travel; Sign(0) is +1, and a straight shot crosses nothing.
            float signedAngle = Mathf.Sign(aim.x) * _brush.Angle;

            for (int column = 0; column < _spawner.BoardColumns; column++)
            {
                int bit = 1 << column;
                if (column == hitColumn || (brushed & bit) != 0 || !_spawner.TryPeekFrontCube(column, out CubeView front))
                {
                    continue;
                }

                float entry = FlightPath.EnterFraction(from, to, front.transform.position, _spawner.CellSize);
                if (entry < 0f || entry > progress)
                {
                    continue;
                }

                front.Nudge(signedAngle, _brush.Duration, _brush.Shape);
                brushed |= bit;
            }

            return brushed;
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

            /// <summary>
            /// How far in front of the shooter, along its aim, the shot leaves from. At zero
            /// the splash and the bullet spawn inside the shooter's own body.
            /// </summary>
            [Tooltip("World units in front of the shooter, along its aim, where the muzzle splash and the bullet appear. Zero puts them inside the shooter's body; about half a cube clears it.")]
            public float MuzzleReach;

            /// <summary>How long a bullet flies to its cube.</summary>
            [Tooltip("Seconds a bullet takes to reach its cube. The cube dies on arrival.")]
            public float FlightDuration;

            /// <summary>
            /// How far back along the flight line the impact splash sits, so it plays on the
            /// cube's near face instead of buried in its middle. Negative pushes it through.
            /// </summary>
            [Tooltip("World units the impact splash is pulled back from the cube's centre toward the shooter. Roughly half a cube puts it on the face; negative buries it deeper.")]
            public float ImpactPullback;

            /// <summary>
            /// How far above the cube's centre the impact splash sits. At zero it plays inside
            /// the cube, where the neighbours hide most of it; half a cube puts it on the top face.
            /// </summary>
            [Tooltip("World units the impact splash is lifted above the cube's centre. Zero plays it inside the cube; about half a cube puts it on the top face, in view.")]
            public float ImpactLift;

            /// <summary>The values a fresh director starts with.</summary>
            public static Firing Defaults => new Firing
            {
                Interval = 0.12f,
                MuzzleHeight = 0.5f,
                MuzzleReach = 0.6f,
                FlightDuration = 0.17f,
                ImpactPullback = 0.45f,
                ImpactLift = 0.5f,
            };
        }

        /// <summary>What happens to the board when a cube is hit. One inspector heading.</summary>
        [Serializable]
        public struct CubeDeath
        {
            /// <summary>How long the collapse takes, from impact until the cube is gone.</summary>
            [Tooltip("Seconds from impact until the cube is gone. The curve below spends this time.")]
            public float CollapseDuration;

            /// <summary>The death's shape: x is the fraction of CollapseDuration, y is the cube's scale as a multiple of its scale at impact.</summary>
            [Tooltip("The cube's scale over the collapse. X: 0 at impact, 1 at the end. Y: multiple of the scale at impact - start at 1, end at 0, go above 1 for a flinch. Editable in Play.")]
            public AnimationCurve CollapseCurve;

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
                // (1 - t)^2: the original's OutQuad shrink, as a curve the inspector can reshape.
                CollapseCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -2f), new Keyframe(1f, 0f, 0f, 0f)),
                FlowDuration = 0.3f,
                SettleOvershoot = 1.7f,
            };
        }

        /// <summary>What a cube does when a bullet passes through it on the way to another. One inspector heading.</summary>
        [Serializable]
        public struct Brush
        {
            /// <summary>The yaw at full lean, in degrees. The original measures 15-20 on a passing shot.</summary>
            [Tooltip("Degrees a brushed cube turns about up at the peak of the lean. The original: 15-20. Zero switches the brush off.")]
            public float Angle;

            /// <summary>How long one brush lasts, lean and return included.</summary>
            [Tooltip("Seconds one brush lasts, from the bullet crossing the face until the cube is upright again.")]
            public float Duration;

            /// <summary>The lean over the brush: x is the fraction of Duration, y the multiple of Angle. Ends at 0 so the cube stands upright.</summary>
            [Tooltip("The lean over the brush. X: 0 when the bullet crosses the face, 1 at the end. Y: multiple of Angle - peak early, cross zero, a small swing back, end at 0. Editable in Play.")]
            public AnimationCurve Shape;

            /// <summary>The values a fresh director starts with - the original's, measured frame by frame at 25 fps.</summary>
            public static Brush Defaults => new Brush
            {
                Angle = 18f,
                Duration = 0.2f,
                // Peak at 40 ms, upright by 100 ms, a small counter-swing, at rest by 200 ms.
                Shape = new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.5f, 0f), new Keyframe(0.7f, -0.3f), new Keyframe(1f, 0f)),
            };
        }

        #endregion
    }
}
