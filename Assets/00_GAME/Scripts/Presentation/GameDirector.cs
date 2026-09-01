// GameDirector - the async glue: input in, GameLoop calls out, animations around both.
// Layer: Presentation.
// Responsibility: turning a tap into a TrySelect, pacing each seated shooter's TryShoot
//   calls, and dressing every result - the run to the slot, the queue step-up, the cube
//   death and flow, the drained shooter's exit.
// NOT its responsibility: a single game rule. Every decision is a GameLoop call; if this
//   file ever contains an if about colours, ammo or verdicts beyond relaying them, that
//   logic has leaked out of the testable layer.
//
// Why each fire loop counts its own ammo instead of watching the slot: the slot frees on
// the last shot and the PLAYER may seat a new shooter into it before this loop's next
// tick. A loop keyed on occupancy would then keep firing with the new tenant's ammo and
// the old tenant's view. The local count makes each loop fire exactly its own shots.

using System;
using Blast.Application;
using Blast.Domain;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blast.Presentation
{
    /// <summary>Drives the play session: taps, fire rhythms and the visuals around them.</summary>
    public sealed class GameDirector : MonoBehaviour
    {
        #region Fields

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

        /// <summary>The camera taps are raycast from.</summary>
        Camera _camera;

        /// <summary>Whether the verdict has been announced; it only happens once.</summary>
        bool _verdictAnnounced;

        #endregion

        #region Public Methods

        /// <summary>Receives the loop, the slots and the registry this director drives.</summary>
        /// <param name="loop">The use case every action goes through.</param>
        /// <param name="slots">The slot row, for ammo reads.</param>
        /// <param name="spawner">The view registry.</param>
        public void Construct(GameLoop loop, SlotRow slots, LevelSpawner spawner)
        {
            _loop = loop;
            _slots = slots;
            _spawner = spawner;
            _camera = Camera.main;
        }

        #endregion

        #region Private Methods

        /// <summary>Turns a tap on a front-row shooter into a selection.</summary>
        void Update()
        {
            // Construct runs from the scope's Configure; a frame can arrive before it.
            if (_loop == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                return;
            }

            ShooterView tapped = hit.collider.GetComponentInParent<ShooterView>();
            if (tapped == null || !_spawner.TryGetSelectableColumn(tapped, out int column))
            {
                return;
            }

            OnSelected(column).Forget();
        }

        /// <summary>Runs one selection: seat in the domain, then animate the consequences.</summary>
        /// <param name="column">The queue column the player tapped.</param>
        async UniTaskVoid OnSelected(int column)
        {
            if (!_loop.TrySelect(column, out int slot))
            {
                return;
            }

            // Domain first, views second: the ammo read below must see the seated shooter.
            int ammo = _slots.AmmoAt(slot);
            ShooterView view = _spawner.PopFrontShooter(column);
            _spawner.StepQueueForward(column, _stepDuration);

            AnnounceIfDecided();

            await view.transform.DOMove(_spawner.SlotWorldPosition(slot), _runDuration).SetEase(Ease.OutQuad);

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
                    return;
                }

                if (_loop.TryShoot(slot, out int hitColumn))
                {
                    ammo--;
                    view.SetAmmo(ammo);
                    KillFrontCube(hitColumn).Forget();
                    _spawner.FlowBoardColumn(hitColumn, _flowDuration);
                    AnnounceIfDecided();
                }

                // One rhythm for firing and for waiting: a targetless shooter re-checks at
                // the same rate it would have fired, which reads naturally on screen.
                await UniTask.Delay(TimeSpan.FromSeconds(_fireInterval));
            }

            await Leave(view);
        }

        /// <summary>Shrinks the dead front cube of a column away and despawns it.</summary>
        /// <param name="column">The board column that was hit.</param>
        async UniTaskVoid KillFrontCube(int column)
        {
            CubeView cube = _spawner.PopFrontCube(column);

            await cube.transform.DOScale(Vector3.zero, _cubeDeathDuration).SetEase(Ease.InBack);

            Destroy(cube.gameObject);
        }

        /// <summary>Runs a drained shooter off-screen and despawns it.</summary>
        /// <param name="view">The shooter's visual.</param>
        async UniTask Leave(ShooterView view)
        {
            Vector3 offScreen = view.transform.position + new Vector3(0f, 0f, -_leaveDistance);

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
