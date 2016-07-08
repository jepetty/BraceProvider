using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BraceProvider
{
    class BraceBlockContextSource : IBlockContextSource
    {
        private ITextBuffer _buffer;

        public BraceBlockContextSource(ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        public async Task<IBlockContext> GetBlockContextAsync(IBlockTag blockTag, ITextView view, CancellationToken token)
        {
            IBlockContext bc = null;
            await Task.Run(() => {
                if (view.TextBuffer == _buffer)
                {
                    object context = CreateContext(blockTag, view);
                    bc = new BraceBlockContext(blockTag, view, context);
                }
            });

            return bc;
        }

        private object CreateContext(IBlockTag blockTag, ITextView view)
        {
            IBlockTag tempTag = blockTag;
            Stack<IBlockTag> stack = new Stack<IBlockTag>();
            while (true)
            {
                if (tempTag.Level == -1)
                    break;
                stack.Push(tempTag);
                if (tempTag.Level != 0)
                {
                    tempTag = tempTag.Parent;
                }
            }

            int indent = 0;
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                tempTag = stack.Pop();
                builder.Append(GetStatement(tempTag));

                indent += 1;
                if (stack.Count != 0)
                {
                    builder.Append('\r');
                    builder.Append(' ', indent);
                }
                else
                    break;
            }

            return builder.ToString();
        }

        private string GetStatement(IBlockTag tag)
        {
            // Handle the two separate cases:
            // public void Foo() {
            // |
            //
            // public void Bar()
            // {
            // |
            int lineNumber = _buffer.CurrentSnapshot.GetLineNumberFromPosition(tag.Span.Start.Position);
            ITextSnapshotLine line = _buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);
            if (FirstNonWhitespace(line))
            {
                line = _buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber - 1);
            }
            return line.GetText();
        }

        // returns true if the line starts with '{' (and isn't the statement), false otherwise
        private bool FirstNonWhitespace(ITextSnapshotLine line)
        {
            int i = line.Start;
            while (i < line.End)
            {
                char c = line.Snapshot[i];
                if (!char.IsWhiteSpace(c))
                {
                    if (c == '{')
                        return true;
                    else
                        return false;
                }
                i++;
            }
            return false;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    class BraceBlockContext : IBlockContext
    {
        private object _content;
        private IBlockTag _tag;
        private ITextView _view;

        public BraceBlockContext(IBlockTag tag, ITextView view, object content)
        {
            _tag = tag;
            _view = view;
            _content = content;
        }

        public object Content
        {
            get { return _content; }
        }

        public IBlockTag BlockTag
        {
            get { return _tag; }
        }

        public ITextView TextView
        {
            get { return _view; }
        }
    }
}
