﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Alt.Internals
{
    public abstract class LexerBase
    {
        protected const string PLEASE_REPORT_BUG_MSG = " This indicates a bug in Schyntax. Please open an issue on github.";

        protected enum ContextMode
        {
            Program,
            Group,
            Expression,
        }

        private readonly Stack<ContextMode> _contextStack = new Stack<ContextMode>();

        protected ContextMode Context => _contextStack.Peek();

        public string Input { get; }
        protected delegate LexMethod LexMethod();

        protected int _index = 0;
        protected readonly int _length;
        protected string _leadingTrivia = "";
        private readonly Queue<Token> _tokenQueue = new Queue<Token>();

        protected LexMethod _lexMethod;

        protected LexerBase(string input)
        {
            Input = input;
            _length = input.Length;
            EnterContext(ContextMode.Program);
        }

        public Token Advance()
        {
            if (_tokenQueue.Count == 0)
                QueueNext();

            return _tokenQueue.Dequeue();
        }

        public Token Peek()
        {
            if (_tokenQueue.Count == 0)
                QueueNext();

            return _tokenQueue.Peek();
        }

        private void QueueNext()
        {
            while (_tokenQueue.Count == 0)
            {
                ConsumeWhiteSpace();
                _lexMethod = _lexMethod();
            }
        }

        protected void EnterContext(ContextMode mode)
        {
            _contextStack.Push(mode);
        }

        protected ContextMode ExitContext()
        {
            if (_contextStack.Count == 1)
                throw new Exception("The lexer attempted to exit the last context." + PLEASE_REPORT_BUG_MSG);

            return _contextStack.Pop();
        }

        private bool IsEndNext => _index == _length;
        private bool IsWhiteSpaceNext => IsWhiteSpace(_index);

        private bool IsWhiteSpace(int index)
        {
            switch (Input[_index])
            {
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    return true;
                default:
                    return false;
            }
        }

        protected bool EndOfInput()
        {
            ConsumeWhiteSpace();
            if (IsEndNext)
            {
                if (_contextStack.Count > 1)
                    throw new Exception("Lexer reached the end of the input while in a nested context." + PLEASE_REPORT_BUG_MSG);

                ConsumeToken(new Token() { Type = TokenType.EndOfInput, Index = _index, RawValue = "", Value = "" });
                return true;
            }

            return false;
        }

        protected void ConsumeWhiteSpace()
        {
            var start = _index;
            while (!IsEndNext && IsWhiteSpaceNext)
                _index++;

            _leadingTrivia += Input.Substring(start, _index - start);
        }

        internal bool IsNextTerm(Terms.Terminal term)
        {
            ConsumeWhiteSpace();
            return term.GetToken(Input, _index) != null;
        }

        internal void ConsumeTerm(Terms.Terminal term)
        {
            ConsumeWhiteSpace();

            var tok = term.GetToken(Input, _index);
            if (tok == null)
                ThrowUnexpectedText(term.TokenType);

            ConsumeToken(tok);
        }

        internal bool ConsumeOptionalTerm(Terms.Terminal term)
        {
            ConsumeWhiteSpace();

            var tok = term.GetToken(Input, _index);
            if (tok == null)
                return false;

            ConsumeToken(tok);
            return true;
        }

        private void ConsumeToken(Token tok)
        {
            _index += tok.RawValue.Length;
            tok.LeadingTrivia = _leadingTrivia;
            _leadingTrivia = "";
            _tokenQueue.Enqueue(tok);
        }

        protected void ThrowUnexpectedText(params TokenType[] expectedTokenTypes)
        {
            var msg = String.Format("Unexpected input. Was expecting one of: {0}\n\n{1}", String.Join(", ", expectedTokenTypes), GetPointerToIndex(_index));
            throw new SchyntaxParseException(msg, _index, Input);
        }

        internal string GetPointerToIndex(int index)
        {
            var start = Math.Max(0, index - 20);
            var length = Math.Min(Input.Length - start, 50);

            StringBuilder sb = new StringBuilder(73);
            sb.Append(Input.Substring(start, length));
            sb.Append("\n");

            for (var i = start; i < index; i++)
                sb.Append(' ');

            sb.Append('^');
            return sb.ToString();
        }
    }
}