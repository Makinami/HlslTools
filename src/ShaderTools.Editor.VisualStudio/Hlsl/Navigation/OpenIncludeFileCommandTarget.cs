﻿using System;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using ShaderTools.CodeAnalysis.Hlsl.Text;
using ShaderTools.CodeAnalysis.Text;
using ShaderTools.Editor.VisualStudio.Core.Navigation;
using ShaderTools.Editor.VisualStudio.Core.Text;
using ShaderTools.Editor.VisualStudio.Core.Util;
using ShaderTools.Editor.VisualStudio.Hlsl.Util.Extensions;

namespace ShaderTools.Editor.VisualStudio.Hlsl.Navigation
{
    internal sealed class OpenIncludeFileCommandTarget : CommandTargetBase<VSConstants.VSStd2KCmdID>
    {
        private readonly IWpfTextView _textView;
        private readonly IServiceProvider _serviceProvider;

        public OpenIncludeFileCommandTarget(IVsTextView adapter, IWpfTextView textView, IServiceProvider serviceProvider)
            : base(adapter, textView, VSConstants.VSStd2KCmdID.OPENFILE)
        {
            _textView = textView;
            _serviceProvider = serviceProvider;
        }

        protected override bool IsEnabled(VSConstants.VSStd2KCmdID commandId, ref string commandText)
        {
            var includeDirectiveTrivia = GetIncludeDirective();
            if (includeDirectiveTrivia == null)
                return false;

            commandText = $"Open Document {includeDirectiveTrivia.Filename.Text}";
            return true;
        }

        protected override bool Execute(VSConstants.VSStd2KCmdID commandId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var includeDirectiveTrivia = GetIncludeDirective();
            if (includeDirectiveTrivia == null)
                return false;

            var includeFileResolver = new IncludeFileResolver(_textView.TextBuffer.GetIncludeFileSystem());

            var include = includeFileResolver.OpenInclude(
                includeDirectiveTrivia.TrimmedFilename,
                new CodeAnalysis.Text.SourceFile(_textView.TextBuffer.CurrentSnapshot.ToSourceText(), null),
                _textView.TextBuffer.GetConfigFile().HlslAdditionalIncludeDirectories);

            if (include == null)
                return false;

            _serviceProvider.NavigateTo(include.FilePath, 0, 0, 0, 0);
            return true;
        }

        private IncludeDirectiveTriviaSyntax GetIncludeDirective()
        {
            var pos = _textView.Caret.Position.BufferPosition;
            var document = pos.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            var syntaxTree = document.GetSyntaxTreeSynchronously(CancellationToken.None);
            var sourceLocation = syntaxTree.MapRootFilePosition(pos.Position);
            var syntaxToken = ((SyntaxNode) syntaxTree.Root).FindToken(sourceLocation, true);

            if (syntaxToken.Parent == null || syntaxToken.Parent.Kind != SyntaxKind.IncludeDirectiveTrivia)
                return null;

            var includeDirectiveTrivia = (IncludeDirectiveTriviaSyntax)syntaxToken.Parent;
            if (includeDirectiveTrivia.Filename != syntaxToken)
                return null;

            return includeDirectiveTrivia;
        }
    }
}