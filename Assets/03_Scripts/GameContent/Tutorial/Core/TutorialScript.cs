using System;
using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 대본 전체. 메인 비트는 넣은 순서가 곧 진행 순서다.
    /// 생성 시 참조 무결성을 검사해, 잘못된 대본은 플레이 도중이 아니라 시작 시점에 실패한다.
    /// </summary>
    public sealed class TutorialScript
    {
        private readonly List<TutorialBeat> _mains = new List<TutorialBeat>();
        private readonly List<TutorialBeat> _sides = new List<TutorialBeat>();
        private readonly Dictionary<string, TutorialBeat> _byId = new Dictionary<string, TutorialBeat>();
        private readonly Dictionary<string, int> _mainIndex = new Dictionary<string, int>();

        public TutorialScript(IEnumerable<TutorialBeat> beats)
        {
            if (beats == null)
            {
                throw new ArgumentNullException(nameof(beats));
            }

            foreach (TutorialBeat beat in beats)
            {
                if (beat == null)
                {
                    throw new ArgumentException("null 비트가 있다", nameof(beats));
                }

                if (_byId.ContainsKey(beat.Id))
                {
                    throw new ArgumentException("비트 번호가 겹친다: " + beat.Id, nameof(beats));
                }

                _byId.Add(beat.Id, beat);

                if (beat.Kind == TutorialBeatKind.Main)
                {
                    _mainIndex.Add(beat.Id, _mains.Count);
                    _mains.Add(beat);
                }
                else
                {
                    _sides.Add(beat);
                }
            }

            if (_mains.Count == 0)
            {
                throw new ArgumentException("메인 비트가 하나도 없다", nameof(beats));
            }

            Validate();
        }

        public IReadOnlyList<TutorialBeat> Mains
        {
            get { return _mains; }
        }

        public IReadOnlyList<TutorialBeat> Sides
        {
            get { return _sides; }
        }

        public bool TryGetBeat(string id, out TutorialBeat beat)
        {
            if (id == null)
            {
                beat = null;
                return false;
            }

            return _byId.TryGetValue(id, out beat);
        }

        /// <summary>메인 비트의 순서 번호. 메인이 아니면 -1.</summary>
        public int IndexOfMain(string id)
        {
            int index;
            return id != null && _mainIndex.TryGetValue(id, out index) ? index : -1;
        }

        /// <summary>모든 대사(대체 대사 포함)를 한 번씩 돈다. 검증·사전 처리용.</summary>
        public IEnumerable<TutorialLine> AllLines()
        {
            foreach (TutorialBeat beat in _byId.Values)
            {
                for (int i = 0; i < beat.Lines.Length; i++)
                {
                    yield return beat.Lines[i];
                }

                if (beat.VariantLines == null)
                {
                    continue;
                }

                for (int i = 0; i < beat.VariantLines.Length; i++)
                {
                    yield return beat.VariantLines[i];
                }
            }
        }

        private void Validate()
        {
            for (int i = 0; i < _mains.Count; i++)
            {
                TutorialBeat beat = _mains[i];

                if (beat.RequiresPlayed != null)
                {
                    int required = IndexOfMain(beat.RequiresPlayed);

                    if (required < 0 || required >= i)
                    {
                        throw new ArgumentException(
                            beat.Id + " 의 RequiresPlayed(" + beat.RequiresPlayed + ")는 앞선 메인 비트여야 한다");
                    }
                }

                if (beat.VariantWhenSidePlayed != null)
                {
                    TutorialBeat side;

                    if (!TryGetBeat(beat.VariantWhenSidePlayed, out side) || side.Kind != TutorialBeatKind.Side)
                    {
                        throw new ArgumentException(
                            beat.Id + " 의 대체 조건(" + beat.VariantWhenSidePlayed + ")은 곁가지 비트여야 한다");
                    }
                }
            }

            for (int i = 0; i < _sides.Count; i++)
            {
                TutorialBeat side = _sides[i];
                int opens = side.OpensAfter == null ? -1 : IndexOfMain(side.OpensAfter);
                int closes = side.ClosesAt == null ? _mains.Count : IndexOfMain(side.ClosesAt);

                if (side.OpensAfter != null && opens < 0)
                {
                    throw new ArgumentException(side.Id + " 의 OpensAfter(" + side.OpensAfter + ")가 메인 비트가 아니다");
                }

                if (side.ClosesAt != null && closes < 0)
                {
                    throw new ArgumentException(side.Id + " 의 ClosesAt(" + side.ClosesAt + ")가 메인 비트가 아니다");
                }

                if (closes <= opens)
                {
                    throw new ArgumentException(side.Id + " 의 창이 비었다(닫힘이 열림보다 앞선다)");
                }
            }
        }
    }
}
