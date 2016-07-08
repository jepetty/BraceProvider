using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BraceProvider
{
    internal sealed class GenericBlockTagger : ITagger<IBlockTag>
    {
        private ITextBuffer _buffer;
        private BraceParser _parser;
        private BackgroundScan _scan;
        private BraceBlockTag _root;
        private int _refCount;

        public void AddRef()
        {
            if (++_refCount == 1)
            {
                _buffer.Changed += OnChanged;
                this.ScanBuffer(_buffer.CurrentSnapshot);
            }
        }

        public void Release()
        {
            if (--_refCount == 0)
            {
                _buffer.Changed -= OnChanged;

                if (_scan != null)
                {
                    _scan.Cancel();
                    _scan = null;
                }

                _root = null;
            }
        }

        private void OnChanged(object sender, TextContentChangedEventArgs e)
        {
            if (AnyTextChanges(e.BeforeVersion, e.After.Version))
                this.ScanBuffer(e.After);
        }

        private static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion)
        {
            while (oldVersion != currentVersion)
            {
                if (oldVersion.Changes.Count > 0)
                    return true;
                oldVersion = oldVersion.Next;
            }

            return false;
        }

        private void ScanBuffer(ITextSnapshot snapshot)
        {
            if (_scan != null)
            {
                _scan.Cancel();
                _scan = null;
            }

            _scan = new BackgroundScan(snapshot, _parser,
                                        delegate (BraceBlockTag newRoot)
                                        {
                                            _root = newRoot;
                                            this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
                                        });
        }

        public GenericBlockTagger(ITextBuffer buffer, BraceParser parser)
        {
            _buffer = buffer;
            _parser = parser;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IBlockTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            BraceBlockTag root = _root;
            if (root != null)
            {
                if (root.Span.Snapshot != spans[0].Snapshot)
                {
                    IList<SnapshotSpan> translatedSpans = new List<SnapshotSpan>(spans.Count);
                    foreach (var span in spans)
                        translatedSpans.Add(span.TranslateTo(root.Span.Snapshot, SpanTrackingMode.EdgeExclusive));

                    spans = new NormalizedSnapshotSpanCollection(translatedSpans);
                }

                foreach (var child in root.Children)
                {
                    foreach (var tag in GetTags(child, spans))
                        yield return tag;
                }
            }
        }

        private static IEnumerable<ITagSpan<IBlockTag>> GetTags(BraceBlockTag block, NormalizedSnapshotSpanCollection spans)
        {
            if (spans.IntersectsWith(new NormalizedSnapshotSpanCollection(block.Span)))
            {
                yield return new TagSpan<IBlockTag>(block.Span, block);

                foreach (var child in block.Children)
                {
                    foreach (var tag in GetTags(child, spans))
                        yield return tag;
                }
            }
        }
    }

    internal class BackgroundScan
    {
        public CancellationTokenSource CancellationSource = new CancellationTokenSource();

        public delegate void CompletionCallback(BraceBlockTag root);

        public BackgroundScan(ITextSnapshot snapshot, BraceParser parser, CompletionCallback completionCallback)
        {
            Task.Run(async delegate
            {
                BraceBlockTag newRoot = await parser.ParseAsync(snapshot, this.CancellationSource.Token);

                if ((newRoot != null) && !this.CancellationSource.Token.IsCancellationRequested)
                    completionCallback(newRoot);
            });
        }

        public void Cancel()
        {
            if (this.CancellationSource != null)
            {
                this.CancellationSource.Cancel();
                this.CancellationSource.Dispose();
            }
        }
    }
}
