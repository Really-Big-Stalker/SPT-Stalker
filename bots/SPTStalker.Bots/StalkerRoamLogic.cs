using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace SPTStalker.Bots
{
    /// <summary>
    /// The roaming itself. The group leader picks a destination (usually just outside one of this raid's
    /// anomaly fields, otherwise a random reachable spot), walks there, searches around for a while, then picks
    /// the next one. Followers keep a loose formation around the leader.
    /// </summary>
    internal class StalkerRoamLogic : CustomLogic
    {
        private enum State { Choosing, Moving, Searching }

        private State _state = State.Choosing;
        private Vector3 _target;
        private float _searchUntil;
        private float _repathAt;
        private Vector3 _progressPos;
        private float _progressAt;
        private Vector3 _formationOffset;
        private Vector3 _lookPoint;
        private float _lookAt;

        public StalkerRoamLogic(BotOwner botOwner) : base(botOwner)
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Random.Range(3f, 6f);
            _formationOffset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        public override void Start()
        {
            _state = State.Choosing;
            BotOwner.SetPose(1f);
            BotOwner.Mover.SetTargetMoveSpeed(Plugin.MoveSpeed.Value);
            BotOwner.Mover.Sprint(false, true);
        }

        public override void Stop()
        {
            BotOwner.SetPose(1f);
        }

        public override void Update(CustomLayer.ActionData data)
        {
            BotOwner.DoorOpener.ManualUpdate();

            var boss = BotOwner.BotFollower != null && BotOwner.BotFollower.HaveBoss ? BotOwner.BotFollower.BossToFollow : null;
            if (boss != null && boss.IsAlive)
            {
                Follow(boss.Position);
            }
            else
            {
                Lead();
            }
        }

        // ---------------------------------------------------------------- leader

        private void Lead()
        {
            switch (_state)
            {
                case State.Choosing:
                    _target = RoamTargets.Pick(BotOwner.Position);
                    _state = State.Moving;
                    _repathAt = 0;
                    _progressPos = BotOwner.Position;
                    _progressAt = Time.time;
                    BotOwner.SetPose(1f);
                    Note($"heading to {_target} ({Flat(BotOwner.Position, _target):0} m)");
                    break;

                case State.Moving:
                    if (Flat(BotOwner.Position, _target) < 4f)
                    {
                        _state = State.Searching;
                        _searchUntil = Time.time + Random.Range(Plugin.SearchSecondsMin.Value, Mathf.Max(Plugin.SearchSecondsMin.Value, Plugin.SearchSecondsMax.Value));
                        BotOwner.Mover.Stop();
                        BotOwner.SetPose(Random.Range(0.5f, 1f));
                        Note("arrived, searching");
                        break;
                    }

                    MoveTo(_target);
                    if (Stuck())
                    {
                        Note("stuck, choosing another destination");
                        _state = State.Choosing;
                    }

                    break;

                case State.Searching:
                    LookAround();
                    if (Time.time > _searchUntil)
                    {
                        _state = State.Choosing;
                    }

                    break;
            }
        }

        // ---------------------------------------------------------------- follower

        private void Follow(Vector3 bossPos)
        {
            var spot = bossPos + _formationOffset;
            if (Flat(BotOwner.Position, bossPos) > 8f)
            {
                MoveTo(spot);
            }
            else
            {
                if (BotOwner.Mover.IsMoving && Flat(BotOwner.Position, spot) < 2f)
                {
                    BotOwner.Mover.Stop();
                }

                LookAround();
            }
        }

        // ---------------------------------------------------------------- helpers

        private void MoveTo(Vector3 point)
        {
            BotOwner.Steering.LookToMovingDirection();
            if (Time.time < _repathAt)
            {
                return;
            }

            _repathAt = Time.time + 2f;
            BotOwner.Mover.SetTargetMoveSpeed(Plugin.MoveSpeed.Value);
            BotOwner.Mover.GoToPoint(point, true, 2f, true, false, false, false);
        }

        private bool Stuck()
        {
            if (Time.time - _progressAt < 15f)
            {
                return false;
            }

            var moved = Flat(BotOwner.Position, _progressPos) > 2f;
            _progressPos = BotOwner.Position;
            _progressAt = Time.time;
            return !moved;
        }

        private void LookAround()
        {
            if (Time.time > _lookAt)
            {
                _lookAt = Time.time + Random.Range(3f, 8f);
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                _lookPoint = BotOwner.Position + dir * 15f + Vector3.up * 1.5f;
            }

            BotOwner.Steering.LookToPoint(_lookPoint);
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private void Note(string message)
        {
            if (Plugin.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"[{BotOwner.Profile?.Nickname}] {message}");
            }
        }
    }
}
