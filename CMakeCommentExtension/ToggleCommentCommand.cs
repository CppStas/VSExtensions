using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.Shell;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CMakeCommentExtension
{
    internal static class CMakeConstants
    {
        public const string FilePattern = @"(?i)\.cmake$|^CMakeLists\.txt$";
    }

    [VisualStudioContribution]
    internal class ToggleCommentCommand : Command
    {
        private const char CommentToken = '#';
        private const string CommentPrefix = "# ";

        private readonly TraceSource logger;

        public ToggleCommentCommand(TraceSource traceSource)
        {
            this.logger = Requires.NotNull(traceSource, nameof(traceSource));
        }

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%CMakeCommentExtension.ToggleComment.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
            Shortcuts = [
                new CommandShortcutConfiguration(ModifierKey.ControlLeftAlt, Key.VK_OEM_2),
            ],
            EnabledWhen = ActivationConstraint.ClientContext(
                          ClientContextKey.Shell.ActiveEditorFileName,
                          CMakeConstants.FilePattern),
        };

        /// <inheritdoc />
        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Use InitializeAsync for any one-time setup or initialization.
            return base.InitializeAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            var textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null)
            {
                return;
            }

            var fileName = Path.GetFileName(textView.Document.Uri.LocalPath);
            if (!IsCMakeFile(fileName))
            {
                return;
            }

            var snapshot = textView.Document;

            var selection = textView.Selection.Extent;
            var firstLineNumber = snapshot.GetLineNumberFromPosition(selection.Start);
            var lastLineNumber = snapshot.GetLineNumberFromPosition(selection.End);

            // VS quirk: when a multi-line selection ends exactly at the start of a
            // line, the user almost certainly does not mean "include that line".
            if (lastLineNumber > firstLineNumber &&
                snapshot.Lines[lastLineNumber].Text.Start == selection.End)
            {
                lastLineNumber--;
            }

            bool shouldUncomment = AllNonBlankLinesAreCommented(
                snapshot.Lines,
                firstLineNumber,
                lastLineNumber);

            var activeBefore = textView.Selection.ActivePosition;
            var anchorBefore = textView.Selection.AnchorPosition;
            var insertBefore = textView.Selection.InsertionPosition;
            var activeLine = snapshot.GetLineNumberFromPosition(activeBefore);
            var anchorLine = snapshot.GetLineNumberFromPosition(anchorBefore);
            var insertLine = snapshot.GetLineNumberFromPosition(insertBefore);

            await this.Extensibility.Editor().EditAsync(batch =>
            {
                ITextDocumentEditor docEditor = snapshot.AsEditable(batch);

                for (var lineNumber = firstLineNumber; lineNumber <= lastLineNumber; lineNumber++)
                {
                    ITextDocumentSnapshotLine line = snapshot.Lines[lineNumber];
                    if (shouldUncomment)
                    {
                        UncommentLine(docEditor, line);
                    }
                    else
                    {
                        CommentLine(docEditor, line);
                    }
                }

                if (!shouldUncomment)
                {
                    (bool, TextPosition) getOffset(TextPosition textPosition, int lineNumber)
                    {
                        var line = snapshot.Lines[lineNumber];
                        var indent = FirstNonWhitespaceIndex(line.Text);
                        if (indent < 0 || line.Text.Start == textPosition)
                        {
                            return (true, textPosition);
                        }
                        int offsetLength = line.Text.StartsWith(CommentPrefix) ? CommentPrefix.Length : line.Text[0] == CommentToken ? 1 : 0;
                        return (false, textPosition + offsetLength);
                    }

                    var (activeAtLineStart, activeNew) = getOffset(activeBefore, activeLine);
                    var (anchorAtLineStart, anchorNew) = getOffset(anchorBefore, anchorLine);
                    var (insertAtLineStart, insertNew) = getOffset(insertBefore, insertLine);

                    if (activeAtLineStart || anchorAtLineStart || insertAtLineStart)
                    {

                        ITextViewEditor viewEditor = textView.AsEditable(batch);
                        viewEditor.SetSelections([new Selection(activeNew, anchorNew, insertNew)]);
                    }
                }
            }, cancellationToken);
        }


        private static bool AllNonBlankLinesAreCommented(IReadOnlyList<ITextDocumentSnapshotLine> lines, int firstLineNumber, int lastLineNumber)
        {
            for (int i = firstLineNumber; i <= lastLineNumber; ++i)
            {
                TextRange lineText = lines[i].Text;
                foreach (char ch in lineText)
                {
                    if (!char.IsWhiteSpace(ch))
                    {
                        if (ch != CommentToken)
                        {
                            return false;
                        }
                        break;
                    }
                }
            }
            return true;
        }

        private static void CommentLine(ITextDocumentEditor editor, ITextDocumentSnapshotLine line)
        {
            var indent = FirstNonWhitespaceIndex(line.Text);
            if (indent < 0)
            {
                return;
            }

            editor.Insert(line.Text.Start + indent, CommentPrefix);
        }

        private static void UncommentLine(ITextDocumentEditor editor, ITextDocumentSnapshotLine line)
        {
            var commentTokenIndex = FirstNonWhitespaceIndex(line.Text);
            if (commentTokenIndex < 0 || line.Text[commentTokenIndex] != CommentToken)
            {
                return;
            }

            var deleteLength = 1;
            if (commentTokenIndex + 1 < line.Text.Length && char.IsWhiteSpace(line.Text[commentTokenIndex + 1]))
            {
                deleteLength = 2;
            }

            var deleteStart = line.Text.Start + commentTokenIndex;
            editor.Delete(new TextRange(deleteStart, deleteStart + deleteLength));
        }

        private static int FirstNonWhitespaceIndex(TextRange textRange)
        {
            for (var i = 0; i < textRange.Length; i++)
            {
                if (!char.IsWhiteSpace(textRange[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsCMakeFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            // Use the same regex logic to ensure behavior is identical
            bool val = Regex.IsMatch(fileName, CMakeConstants.FilePattern);
            return val;
        }
    }
}
