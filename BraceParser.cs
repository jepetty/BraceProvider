using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace BraceProvider
{
    internal class BraceParser
    {
        public BraceParser()
        {
        }

        public Task<BraceBlockTag> ParseAsync(ITextSnapshot snapshot, CancellationToken token)
        {
            BraceBlockTag root = new BraceBlockTag(null, new SnapshotSpan(snapshot, 0, snapshot.Length), 0, -1);

            BraceBlockTag parent = root;

            Stack<BraceBlockTag> blockOpenings = new Stack<BraceBlockTag>();

            bool leadingWhitespace = true;
            int statementStart = 0;

            SnapshotFilter filter = new SnapshotFilter(snapshot);
            while (filter.Next())
            {
                int position = filter.Position;
                char c = filter.Character;

                if (leadingWhitespace)
                {
                    leadingWhitespace = char.IsWhiteSpace(c);
                    statementStart = position;
                }

                if (!filter.InQuote)
                {
                    if (c == '{')
                    {
                        BraceBlockTag child = new BraceBlockTag(parent, new SnapshotSpan(snapshot, position, 0), statementStart, blockOpenings.Count + 1);

                        blockOpenings.Push(child);

                        parent = child;
                    }
                    else if (c == '}')
                    {
                        if (blockOpenings.Count > 0)
                        {
                            BraceBlockTag child = blockOpenings.Pop();
                            child.SetSpan(new SnapshotSpan(snapshot, Span.FromBounds(child.Span.Start, position + 1)));

                            parent = child.Parent;
                        }
                    }
                }

                if (filter.EOS)
                {
                    leadingWhitespace = true;
                }

                if (token.IsCancellationRequested)
                    return null;
            }

            while (blockOpenings.Count > 0)
            {
                BraceBlockTag child = blockOpenings.Pop();
                child.SetSpan(new SnapshotSpan(snapshot, Span.FromBounds(child.Span.Start, snapshot.Length)));
            }

            return Task.FromResult<BraceBlockTag>(root);
        }

        private class SnapshotFilter : QuoteFilter
        {
            private bool _eos;
            private int _braceDepth;
            private Stack<int> _nestedBraceDepth = new Stack<int>();

            public SnapshotFilter(ITextSnapshot snapshot)
                : base(snapshot)
            {
            }

            public new bool Next()
            {
                if (!base.Next())
                    return false;

                _eos = false;
                if (!base.InQuote)
                {
                    char c = base.Character;

                    if (c == ';')
                    {
                        //Whether or not a ; counts as an end of statement depends on context.
                        //      foo();                          <--This does
                        //      for (int i = 0; (i < 10); ++i)  <-- These don't
                        //          bar(delegate{
                        //                  baz();              <-- this does
                        //
                        // Basically, it is an end of statement unless it is contained in an open parenthesis and an open brace
                        // hasn't been encountered since the open paranthesis.
                        _eos = (_nestedBraceDepth.Count == 0) || (_nestedBraceDepth.Peek() < _braceDepth);
                    }
                    else if (c == '(')
                    {
                        _nestedBraceDepth.Push(_braceDepth);
                    }
                    else if (c == ')')
                    {
                        if (_nestedBraceDepth.Count > 0)
                            _nestedBraceDepth.Pop();
                    }
                    else if (c == '{')
                    {
                        ++(_braceDepth);
                        _eos = true;
                    }
                    else if (c == '}')
                    {
                        --(_braceDepth);
                        _eos = true;
                    }
                }

                return true;
            }

            public bool EOS { get { return _eos; } }
        }

        private class QuoteFilter : BaseFilter
        {
            private char _quote = ' ';
            private bool _escape;

            public QuoteFilter(ITextSnapshot snapshot)
                : base(snapshot)
            {
            }

            public bool Next()
            {
                if (++(this.position) < this.snapshot.Length)
                {
                    bool wasEscaped = _escape;
                    _escape = false;

                    char opener = base.Character;
                    if (_quote == ' ')
                    {
                        if (opener == '#')
                        {
                            ITextSnapshotLine line = this.snapshot.GetLineFromPosition(this.position);
                            this.position = line.End;
                        }
                        else if ((opener == '\'') || (opener == '\"'))
                            _quote = opener;
                        else if (opener == '@')
                        {
                            char next = this.PeekNextChar();
                            if (next == '\"')
                            {
                                _quote = '@';
                                this.position += 1;
                            }
                        }
                        else if (opener == '/')
                        {
                            char next = this.PeekNextChar();
                            if (next == '/')
                            {
                                ITextSnapshotLine line = this.snapshot.GetLineFromPosition(this.position);
                                this.position = line.End;
                            }
                            else if (next == '*')
                            {
                                this.position += 2;

                                while (this.position < this.snapshot.Length)
                                {
                                    if ((this.snapshot[this.position] == '*') && (this.PeekNextChar() == '/'))
                                    {
                                        this.position += 2;
                                        break;
                                    }

                                    ++(this.position);
                                }
                            }
                        }
                    }
                    else if ((_quote != '@') && (opener == '\\') && !wasEscaped)
                    {
                        _escape = true;
                    }
                    else if (((opener == _quote) || ((opener == '\"') && (_quote == '@'))) && !wasEscaped)
                    {
                        _quote = ' ';
                    }
                    else if ((_quote == '\"') || (_quote == '\''))
                    {
                        ITextSnapshotLine line = this.snapshot.GetLineFromPosition(this.position);
                        if (line.End == this.position)
                        {
                            //End simple quotes at the end of the line.
                            _quote = ' ';
                        }
                    }

                    return (this.position < this.snapshot.Length);
                }

                return false;
            }

            public bool InQuote { get { return (_quote != ' '); } }
        }

        //Return true if statement contains an '=' not contained in (
        public static bool ContainsEquals(string statement)
        {
            int parenthesisDepth = 0;
            for (int i = 0; (i < statement.Length); ++i)
            {
                char c = statement[i];
                if ((c == '=') && (parenthesisDepth == 0))
                    return true;
                else if (c == '(')
                    ++parenthesisDepth;
                else if (c == ')')
                    --parenthesisDepth;
            }

            return false;
        }
    }

    internal class BaseFilter
    {
        protected ITextSnapshot snapshot;
        protected int position;

        public BaseFilter(ITextSnapshot snapshot)
        {
            this.snapshot = snapshot;
            this.position = -1;
        }

        public char Character { get { return this.snapshot[this.position]; } }
        public int Position { get { return this.position; } }

        protected char PeekNextChar()
        {
            return PeekNextChar(1);
        }

        protected char PeekNextChar(int offset)
        {
            int p = this.position + offset;

            if ((0 <= p) && (p < this.snapshot.Length))
                return this.snapshot[p];
            else
                return ' ';
        }
    }
}
