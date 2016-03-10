// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.Windows.Design.Host;

namespace Microsoft.PythonTools.Designer {
    using Infrastructure;
    using AP = AnalysisProtocol;

    class WpfEventBindingProvider : EventBindingProvider {
        private Project.PythonFileNode _pythonFileNode;

        public WpfEventBindingProvider(Project.PythonFileNode pythonFileNode) {
            _pythonFileNode = pythonFileNode;
        }

        public override bool AddEventHandler(EventDescription eventDescription, string objectName, string methodName) {
            // we return false here which causes the event handler to always be wired up via XAML instead of via code.
            return false;
        }

        public override bool AllowClassNameForMethodName() {
            return true;
        }

        public override void AppendStatements(EventDescription eventDescription, string methodName, string statements, int relativePosition) {
            throw new NotImplementedException();
        }

        public override string CodeProviderLanguage {
            get { return "Python"; }
        }

        public override bool CreateMethod(EventDescription eventDescription, string methodName, string initialStatements) {
            // build the new method handler
            var fileInfo = _pythonFileNode.GetAnalysisEntry();
            var insertPoint = fileInfo.Analyzer.GetInsertionPointAsync(
                fileInfo,
                _pythonFileNode.GetTextBuffer(),
                null
            ).WaitAndUnwrapExceptions();

            if (insertPoint != null) {
                var view = _pythonFileNode.GetTextView();
                var textBuffer = _pythonFileNode.GetTextBuffer();
                var translator = insertPoint.GetTracker(insertPoint.Data.version);

                using (var edit = textBuffer.CreateEdit()) {
                    var text = BuildMethod(
                        eventDescription,
                        methodName,
                        new string(' ', insertPoint.Data.indentation),
                        view.Options.IsConvertTabsToSpacesEnabled() ?
                            view.Options.GetIndentSize() :
                            -1);

                    edit.Insert(translator.TranslateForward(insertPoint.Data.location), text);
                    edit.Apply();
                    return true;
                }
            }

            return false;
        }


        private static string BuildMethod(EventDescription eventDescription, string methodName, string indentation, int tabSize) {
            StringBuilder text = new StringBuilder();
            text.AppendLine(indentation);
            text.Append(indentation);
            text.Append("def ");
            text.Append(methodName);
            text.Append('(');
            text.Append("self");
            foreach (var param in eventDescription.Parameters) {
                text.Append(", ");
                text.Append(param.Name);
            }
            text.AppendLine("):");
            if (tabSize < 0) {
                text.Append(indentation);
                text.Append("\tpass");
            } else {
                text.Append(indentation);
                text.Append(' ', tabSize);
                text.Append("pass");
            }
            text.AppendLine();

            return text.ToString();
        }

        public override string CreateUniqueMethodName(string objectName, EventDescription eventDescription) {
            var name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1}", objectName, eventDescription.Name);
            int count = 0;
            var fileInfo = _pythonFileNode.GetAnalysisEntry();

            var methods = fileInfo.Analyzer.FindMethodsAsync(
               fileInfo,
               _pythonFileNode.GetTextBuffer(),
               null,
               null
           ).Result;

            while (methods.Contains(name)) {
                name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1}{2}", objectName, eventDescription.Name, ++count);
            }
            return name;
        }

        public override IEnumerable<string> GetCompatibleMethods(EventDescription eventDescription) {
            var fileInfo = _pythonFileNode.GetAnalysisEntry();
            return fileInfo.Analyzer.FindMethodsAsync(
                fileInfo,
                _pythonFileNode.GetTextBuffer(),
                null,
                eventDescription.Parameters.Count() + 1
            ).Result;
        }

        public override IEnumerable<string> GetMethodHandlers(EventDescription eventDescription, string objectName) {
            return new string[0];
        }

        public override bool IsExistingMethodName(EventDescription eventDescription, string methodName) {
            var fileInfo = _pythonFileNode.GetAnalysisEntry();

            var methods = fileInfo.Analyzer.FindMethodsAsync(
                fileInfo,
                _pythonFileNode.GetTextBuffer(),
                null,
                null
            ).Result;

            return methods.Contains(methodName);
        }

        private AP.MethodInfoResponse FindMethod(string methodName) {
            var fileInfo = _pythonFileNode.GetAnalysisEntry();
            return fileInfo.Analyzer.GetMethodInfoAsync(
                fileInfo,
                _pythonFileNode.GetTextBuffer(),
                null,
                methodName
            ).Result;
        }

        public override bool RemoveEventHandler(EventDescription eventDescription, string objectName, string methodName) {
            var method = FindMethod(methodName);
            if (method != null && method.found) {
                var view = _pythonFileNode.GetTextView();
                var textBuffer = _pythonFileNode.GetTextBuffer();

                // appending a method adds 2 extra newlines, we want to remove those if those are still
                // present so that adding a handler and then removing it leaves the buffer unchanged.

                using (var edit = textBuffer.CreateEdit()) {
                    int start = method.start - 1;

                    // eat the newline we insert before the method
                    while (start >= 0) {
                        var curChar = edit.Snapshot[start];
                        if (!Char.IsWhiteSpace(curChar)) {
                            break;
                        } else if (curChar == ' ' || curChar == '\t') {
                            start--;
                            continue;
                        } else if (curChar == '\n') {
                            if (start != 0) {
                                if (edit.Snapshot[start - 1] == '\r') {
                                    start--;
                                }
                            }
                            start--;
                            break;
                        } else if (curChar == '\r') {
                            start--;
                            break;
                        }

                        start--;
                    }


                    // eat the newline we insert at the end of the method
                    int end = method.end;
                    while (end < edit.Snapshot.Length) {
                        if (edit.Snapshot[end] == '\n') {
                            end++;
                            break;
                        } else if (edit.Snapshot[end] == '\r') {
                            if (end < edit.Snapshot.Length - 1 && edit.Snapshot[end + 1] == '\n') {
                                end += 2;
                            } else {
                                end++;
                            }
                            break;
                        } else if (edit.Snapshot[end] == ' ' || edit.Snapshot[end] == '\t') {
                            end++;
                            continue;
                        } else {
                            break;
                        }
                    }

                    // delete the method and the extra whitespace that we just calculated.
                    edit.Delete(Span.FromBounds(start + 1, end));
                    edit.Apply();
                }

                return true;
            }
            return false;
        }

        public override bool RemoveHandlesForName(string elementName) {
            throw new NotImplementedException();
        }

        public override bool RemoveMethod(EventDescription eventDescription, string methodName) {
            throw new NotImplementedException();
        }

        public override void SetClassName(string className) {
        }

        public override bool ShowMethod(EventDescription eventDescription, string methodName) {
            var method = FindMethod(methodName);
            if (method != null && method.found) {
                var view = _pythonFileNode.GetTextView();
                view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, method.start));
                view.Caret.EnsureVisible();
                return true;
            }

            return false;
        }

        public override void ValidateMethodName(EventDescription eventDescription, string methodName) {
        }
    }
}
