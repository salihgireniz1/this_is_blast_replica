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

        /// <summary>The bullet visual; its body wears the firing shooter's material.</summary>
        [SerializeField] CubeView _bulletPrefab;

        /// <summary>The muzzle flash spawned at every shot. Destroys itself when it stops.</summary>
        [SerializeField] ParticleSystem _splashPrefab;

        /// <summary>Height above a shooter's feet a bullet leaves from.</summary>
        [SerializeField] float _bulletMuzzleHeight = 0.7f;

        /// <summary>How long a bullet flies to its cube.</summary>
        [SerializeField] float _bulletFlightDuration = 0.12f;

        /// <summary>How long a selected shooter runs to its slot.</summary>
        [SerializeField] float _runDuration = 0.45f;

        /// <summary>How long the queue's step-up takes after a selection.</summary>
        [SerializeField] float _stepDuration = 0.25f;

        /// <summary>Seconds between a seated shooter's shots - also its idle re-check rate.</summary>
        [SerializeField] float _fireInterval = 0.22f;

        /// <summary>How long a dying cube shrinks away.</summary>
        [SerializeField] float _cubeDeathDuration = 0.12f;

        /// <summary>How long a column's survivors take to flow one cell forward.</summary>
        [SerializeField] float _flowDuration = 0.15f;

        /// <summary>How long a drained shooter takes to run off-screen.</summary>
        [SerializeField] float _leaveDuration = 0.6f;

        /// <summary>How far off-screen a drained shooter runs before despawning.</summary>
        [SerializeField] float _leaveDistance = 6f;

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
            _spawner.StepQueueForward(column, _stepDuration);

            AnnounceIfDecided();

            view.SetRunning(true);
            await view.transform.DOMove(_spawner.SlotWorldPosition(slot), _runDuration).SetEase(Ease.OutQuad);
            view.SetRunning(false);

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
                    // Targetless: stand idle until a matching cube reaches the front.
                    view.SetRunning(false);
                }

                // One rhythm for firing and for waiting: a targetless shooter re-checks at
                // the same rate it would have fired, which reads naturally on screen.
                await UniTask.Delay(TimeSpan.FromSeconds(_fireInterval));
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

            Vector3 muzzle = shooter.transform.position + Vector3.up * _bulletMuzzleHeight;

            // ponytail: Instantiate/Destroy per shot; pool both in the measured pass.
            // The splash prefab's Stop Action is Destroy, so nothing here has to time it.
            Instantiate(_splashPrefab, muzzle, Quaternion.identity, transform);

            Quaternion bulletFacing = _bulletPrefab.transform.rotation;
            CubeView bullet = Instantiate(_bulletPrefab, muzzle, bulletFacing, transform);
            bullet.Wear(bulletMaterial);

            await bullet.transform.DOMove(cube.transform.position, _bulletFlightDuration).SetEase(Ease.Linear);

            Destroy(bullet.gameObject);
            _spawner.FlowBoardColumn(hitColumn, _flowDuration);

            await cube.transform.DOScale(Vector3.zero, _cubeDeathDuration).SetEase(Ease.InBack);

            Destroy(cube.gameObject);
        }

        /// <summary>Runs a drained shooter off-screen and despawns it.</summary>
        /// <param name="view">The shooter's visual.</param>
        async UniTask Leave(ShooterView view)
        {
            Vector3 offScreen = view.transform.position + new Vector3(0f, 0f, -_leaveDistance);

            view.SetRunning(true);
            await view.transform.DOMove(offScreen, _leaveDuration).SetEase(Ease.InQuad);

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
    }
}
