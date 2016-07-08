using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace BraceProvider
{
    public class BraceBlockTag : IBlockTag
    {
        private SnapshotSpan _span;
        private readonly BraceBlockTag _parent;
        private readonly IList<BraceBlockTag> _children = new List<BraceBlockTag>();
        private readonly int _level;
        private readonly int _statementStart;

        public BraceBlockTag(BraceBlockTag parent, SnapshotSpan span, int statementStart, int level)
        {
            _parent = parent;
            if (parent != null)
            {
                parent._children.Add(this);
            }

            _span = span;
            _statementStart = statementStart;
            _level = level;
        }

        public void SetSpan(SnapshotSpan span)
        {
            _span = span;
        }

        public BraceBlockTag Parent
        {
            get { return _parent; }
        }

        public IList<BraceBlockTag> Children
        {
            get { return _children; }
        }

        public SnapshotSpan Span
        {
            get { return _span; }
        }

        IBlockTag IBlockTag.Parent
        {
            get
            {
                return _parent;
            }
        }

        public int Level
        {
            get { return _level; }
        }

        public SnapshotPoint StatementStart
        {
            get { return new SnapshotPoint(this.Span.Snapshot, _statementStart); }
        }
    }
}
