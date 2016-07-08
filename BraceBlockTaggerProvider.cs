using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace BraceProvider
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(IBlockTag))]
    internal class BraceBlockTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (typeof(T) == typeof(IBlockTag))
            {
                GenericBlockTagger tagger = buffer.Properties.GetOrCreateSingletonProperty(typeof(BraceBlockTaggerProvider), delegate { return new GenericBlockTagger(buffer, new BraceParser()); });

                return new DisposableTagger(tagger) as ITagger<T>;
            }
            else
                return null;
        }
    }

    internal class DisposableTagger : ITagger<IBlockTag>, IDisposable
    {
        private GenericBlockTagger _tagger;
        public DisposableTagger(GenericBlockTagger tagger)
        {
            _tagger = tagger;
            _tagger.AddRef();
            _tagger.TagsChanged += OnTagsChanged;
        }

        private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
        {
            this.TagsChanged?.Invoke(sender, e);
        }

        public IEnumerable<ITagSpan<IBlockTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _tagger.GetTags(spans);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            if (_tagger != null)
            {
                _tagger.TagsChanged -= OnTagsChanged;
                _tagger.Release();
                _tagger = null;
            }
        }
    }
}
