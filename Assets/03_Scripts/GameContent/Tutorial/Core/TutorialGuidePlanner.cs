using System;
using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// "지금 플레이어가 어디로 가야 하는가"를 고른다. 연출 쪽이 그 지점에 연초록 안내 상자를 띄운다.
    /// <list type="number">
    /// <item>걸려 있는 키 안내 문구(최신 우선)의 완료 조건 중 위치 신호 — 예: 3-4 "본부로 돌아갑시다" → 본부.</item>
    /// <item>다음 메인 비트의 트리거 중 위치 신호. 건너뛸 수 있는 비트면 시퀀서와 같은 규칙으로 뒤 비트까지 내다본다
    ///       (다음 단계까지, 닻 비트는 예외). 필수 비트에서는 멈추므로, 플레이어가 앞질러 가도 아직 안 한 가장 이른 필수 단계를 가리킨다.</item>
    /// </list>
    /// 씬에서 위치를 알 수 없는 신호(<paramref name="canResolve"/> false)는 건너뛴다. 할당하지 않는다.
    /// </summary>
    public static class TutorialGuidePlanner
    {
        /// <summary>플레이어가 "그 장소에 가면" 나는 신호인가.</summary>
        public static bool IsLocationSignal(TutorialSignal signal)
        {
            switch (signal)
            {
                case TutorialSignal.ObstacleAhead:
                case TutorialSignal.ObstacleCleared:
                case TutorialSignal.CorridorAEndReached:
                case TutorialSignal.NearSecurityNode:
                case TutorialSignal.NearTerminal:
                case TutorialSignal.BlueDoorAhead:
                case TutorialSignal.LootRoomEntered:
                case TutorialSignal.NearKey:
                case TutorialSignal.VaultCorridorEntered:
                case TutorialSignal.VaultRoomEntered:
                case TutorialSignal.VaultFront:
                case TutorialSignal.PlayerInMainbase:
                case TutorialSignal.SafeInMainbase:
                    return true;
                default:
                    // LootRoomExiting(5-8)은 "방을 나서려 할 때"의 반응 대사라 안내 대상이 아니다.
                    return false;
            }
        }

        /// <summary>
        /// "그 일을 이미 해냈다"는 증거가 되는 신호인가. 위치 신호와 달리 그 자리에 있지 않아도 사실이 남는다.
        /// 시퀀서는 이 신호가 한 번이라도 들어왔으면, 앞 비트가 아직 안 끝났더라도 그 뒤 비트로 건너뛴다.
        /// <para>
        /// 위치 신호(단말기 근처 등)는 일부러 뺐다 — 시작 지점 근처에서 처음부터 참일 수 있어 단계를 잘못 건너뛰게 한다.
        /// </para>
        /// </summary>
        public static bool IsMilestoneSignal(TutorialSignal signal)
        {
            switch (signal)
            {
                case TutorialSignal.SecurityNodeThreatCue:
                case TutorialSignal.SecurityNodeConnected:
                case TutorialSignal.TerminalOpened:
                case TutorialSignal.DoorHackStarted:
                case TutorialSignal.DoorHackSucceeded:
                case TutorialSignal.DoorOpened:
                case TutorialSignal.ItemLooted:
                case TutorialSignal.InventoryThreeFilled:
                case TutorialSignal.KeyAcquired:
                case TutorialSignal.VaultUnlocked:
                case TutorialSignal.VaultHackStarted:
                case TutorialSignal.VaultHackSucceeded:
                case TutorialSignal.CatnipAcquired:
                case TutorialSignal.SettlementShown:
                case TutorialSignal.SettlementRevealed:
                    return true;
                default:
                    return false;
            }
        }

        public static TutorialSignal Choose(TutorialSequencer sequencer, Func<TutorialSignal, bool> canResolve)
        {
            if (sequencer == null || canResolve == null)
            {
                return TutorialSignal.None;
            }

            IReadOnlyList<TutorialHint> hints = sequencer.Hints;

            for (int i = hints.Count - 1; i >= 0; i--)
            {
                TutorialSignal[] completeOn = hints[i].Line.HintCompleteOn;

                for (int k = 0; k < completeOn.Length; k++)
                {
                    if (IsLocationSignal(completeOn[k]) && canResolve(completeOn[k]))
                    {
                        return completeOn[k];
                    }
                }
            }

            int start = sequencer.PendingMainIndex;

            if (start < 0)
            {
                return TutorialSignal.None;
            }

            IReadOnlyList<TutorialBeat> mains = sequencer.Script.Mains;
            int startStage = mains[start].Stage;

            for (int j = start; j < mains.Count; j++)
            {
                TutorialBeat beat = mains[j];
                bool reachable = j == start || beat.Stage <= startStage + 1 || beat.IsAnchor;
                bool requirementMet = beat.RequiresPlayed == null || WasPlayed(sequencer, beat.RequiresPlayed);

                // 대신 나온 비트가 이미 재생돼 곧 건너뛸 비트는 안내하지 않고 넘어간다.
                if (beat.SkippedIfPlayed != null && WasPlayed(sequencer, beat.SkippedIfPlayed))
                {
                    continue;
                }

                if (reachable && requirementMet && sequencer.GetMainState(beat.Id) == TutorialBeatState.Pending)
                {
                    for (int k = 0; k < beat.Triggers.Length; k++)
                    {
                        TutorialSignal trigger = beat.Triggers[k];

                        if (IsLocationSignal(trigger) && canResolve(trigger))
                        {
                            return trigger;
                        }
                    }
                }

                if (!beat.Skippable)
                {
                    break;
                }
            }

            return TutorialSignal.None;
        }

        private static bool WasPlayed(TutorialSequencer sequencer, string beatId)
        {
            TutorialBeatState state = sequencer.GetMainState(beatId);
            return state == TutorialBeatState.Playing || state == TutorialBeatState.Done;
        }
    }
}
