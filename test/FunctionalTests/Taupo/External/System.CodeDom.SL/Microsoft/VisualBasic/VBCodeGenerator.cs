﻿//---------------------------------------------------------------------
// <copyright file="VBCodeGenerator.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.VisualBasic {
    using System.Diagnostics;
    using System;
    using System.IO;
    using System.Collections;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Reflection;
    using System.CodeDom;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Globalization;
    using System.CodeDom.Compiler;
#if !WIN8
    using System.Security.Permissions;
#endif
    using System.Collections.Generic;
    using System.Runtime.Versioning;

    /// <devdoc>
    ///    <para>
    ///       Visual Basic 7 Code Generator.
    ///    </para>
    /// </devdoc>
    public class VBCodeGenerator : CodeGenerator
    {
        private const int MaxLineLength = 80;

        private const GeneratorSupport LanguageSupport = GeneratorSupport.EntryPointMethod |
                                                         GeneratorSupport.GotoStatements |
                                                         GeneratorSupport.ArraysOfArrays |
                                                         GeneratorSupport.MultidimensionalArrays |
                                                         GeneratorSupport.StaticConstructors |
                                                         GeneratorSupport.ReturnTypeAttributes |
                                                         GeneratorSupport.AssemblyAttributes |
                                                         GeneratorSupport.TryCatchStatements |
                                                         GeneratorSupport.DeclareValueTypes |
                                                         GeneratorSupport.DeclareEnums | 
                                                         GeneratorSupport.DeclareEvents | 
                                                         GeneratorSupport.DeclareDelegates |
                                                         GeneratorSupport.DeclareInterfaces |
                                                         GeneratorSupport.ParameterAttributes |
                                                         GeneratorSupport.ReferenceParameters |
                                                         GeneratorSupport.ChainedConstructorArguments |
                                                         GeneratorSupport.NestedTypes |
                                                         GeneratorSupport.MultipleInterfaceMembers |
                                                         GeneratorSupport.PublicStaticMembers |
                                                         GeneratorSupport.ComplexExpressions |
                                                         GeneratorSupport.Win32Resources |
                                                         GeneratorSupport.Resources|
                                                         GeneratorSupport.PartialTypes|
                                                         GeneratorSupport.GenericTypeReference |
                                                         GeneratorSupport.GenericTypeDeclaration |                                                         
                                                         GeneratorSupport.DeclareIndexerProperties;

        private int statementDepth = 0;
        private IDictionary<string, string> provOptions;
            
        // This is the keyword list. To minimize search time and startup time, this is stored by length
        // and then alphabetically for use by FixedStringLookup.Contains.
        private static readonly string[][] keywords = new string[][] {
            null,           // 1 character
            new string[] {  // 2 characters            
                "as",
                "do",
                "if",
                "in",
                "is",
                "me",
                "of",                
                "on",
                "or",
                "to",
            },
            new string[] {  // 3 characters
                "and",
                "dim",
                "end",
                "for",
                "get",
                "let",
                "lib",
                "mod",
                "new",
                "not",
                "rem",
                "set",
                "sub",
                "try",
                "xor",
            },
            new string[] {  // 4 characters
                "ansi",
                "auto",
                "byte",
                "call",
                "case",
                "cdbl",
                "cdec",
                "char",
                "cint",
                "clng",
                "cobj",
                "csng",
                "cstr",
                "date",
                "each",
                "else",
                "enum",
                "exit",
                "goto",
                "like",
                "long",
                "loop",
                "next",
                "step",
                "stop",
                "then",
                "true",
                "wend",
                "when",
                "with",
            },
            new string[] {  // 5 characters  
                "alias",
                "byref",
                "byval",
                "catch",
                "cbool",
                "cbyte",
                "cchar",
                "cdate",
                "class",
                "const",
                "ctype",
                "cuint",
                "culng",                
                "endif",
                "erase",
                "error",
                "event",
                "false",
                "gosub",
                "isnot",                
                "redim",
                "sbyte",
                "short",
                "throw",
                "ulong",
                "until",
                "using",
                "while",
             },
            new string[] {  // 6 characters
                "csbyte",
                "cshort",
                "double",
                "elseif",
                "friend",
                "global",
                "module",
                "mybase",
                "object",
                "option",
                "orelse",
                "public",
                "resume",
                "return",
                "select",
                "shared",
                "single",
                "static",
                "string",
                "typeof",
                "ushort",
            },
            new string[] { // 7 characters
                "andalso",
                "boolean",
                "cushort",
                "decimal",
                "declare",
                "default",
                "finally",
                "gettype",
                "handles",
                "imports",
                "integer",
                "myclass",
                "nothing",
                "partial",
                "private",
                "shadows",
                "trycast",
                "unicode",
                "variant",
            },
            new string[] {  // 8 characters
                "assembly",
                "continue",                    
                "delegate",
                "function",
                "inherits",
                "operator",                
                "optional",
                "preserve",
                "property",
                "readonly",
                "synclock",
                "uinteger",
                "widening"                
            },
            new string [] { // 9 characters
                "addressof",
                "interface",
                "namespace",
                "narrowing",                
                "overloads",
                "overrides",
                "protected",
                "structure",
                "writeonly",
            },
            new string [] { // 10 characters
                "addhandler",
                "directcast",
                "implements",
                "paramarray",
                "raiseevent",
                "withevents",
            },
            new string[] {  // 11 characters
                "mustinherit",
                "overridable",
            },
            new string[] { // 12 characters
                "mustoverride",
            },
            new string [] { // 13 characters
                "removehandler",
            },
            // class_finalize and class_initialize are not keywords anymore,
            // but it will be nice to escape them to avoid warning
            new string [] { // 14 characters
                "class_finalize",
                "notinheritable",
                "notoverridable",
            },
            null,           // 15 characters
            new string [] {
                "class_initialize",
            }
        };

        public VBCodeGenerator() {
        }

        public VBCodeGenerator(IDictionary<string, string> providerOptions)
        {
            provOptions = providerOptions;
        }


#if DEBUG
        static VBCodeGenerator() {
            FixedStringLookup.VerifyLookupTable(keywords, true);

            // Sanity check: try some values;
            Debug.Assert(IsKeyword("for"));
            Debug.Assert(IsKeyword("foR"));
            Debug.Assert(IsKeyword("cdec"));
            Debug.Assert(!IsKeyword("cdab"));
            Debug.Assert(!IsKeyword("cdum"));
        }
#endif

        /// <devdoc>
        ///    <para>
        ///       Tells whether or not the current class should be generated as a module
        ///    </para>
        /// </devdoc>
        private bool IsCurrentModule {
            get {
                return (IsCurrentClass && GetUserData(CurrentClass, "Module", false));
            }
        }
        
        /// <devdoc>
        ///    <para>
        ///       Gets the token that is used to represent <see langword='null'/>.
        ///    </para>
        /// </devdoc>
        protected override string NullToken {
            get {
                return "Nothing";
            }
        }

        private void EnsureInDoubleQuotes(ref bool fInDoubleQuotes, StringBuilder b) {
            if (fInDoubleQuotes) return;
            b.Append("&\"");
            fInDoubleQuotes = true;
        }

        private void EnsureNotInDoubleQuotes(ref bool fInDoubleQuotes, StringBuilder b) {
            if (!fInDoubleQuotes) return;
            b.Append("\"");
            fInDoubleQuotes = false;
        }

        /// <devdoc>
        ///    <para>
        ///       Provides conversion to formatting with escape codes.
        ///    </para>
        /// </devdoc>
        protected override string QuoteSnippetString(string value) {
            StringBuilder b = new StringBuilder(value.Length+5);

            bool fInDoubleQuotes = true;
            Indentation indentObj = new Indentation((IndentedTextWriter)Output, Indent + 1);

            b.Append("\"");

            int i = 0;
            while(i < value.Length) {
                char ch = value[i];
                switch (ch) {
                    case '\"':
                    // These are the inward sloping quotes used by default in some cultures like CHS. 
                    // VBC.EXE does a mapping ANSI that results in it treating these as syntactically equivalent to a
                    // regular double quote.
                    case '\u201C': 
                    case '\u201D':
                    case '\uFF02':
                        EnsureInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append(ch);
                        b.Append(ch);
                        break;
                    case '\r':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        if (i < value.Length - 1 && value[i+1] == '\n') {
                            b.Append("&Global.Microsoft.VisualBasic.ChrW(13)&Global.Microsoft.VisualBasic.ChrW(10)");
                            i++;
                        }
                        else {
                            b.Append("&Global.Microsoft.VisualBasic.ChrW(13)");
                        }
                        break;
                    case '\t':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(9)");
                        break;
                    case '\0':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(0)");
                        break;
                    case '\n':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(10)");
                        break;
                    case '\u2028':
                    case '\u2029':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        AppendEscapedChar(b,ch);
                        break;
                    default:
                        EnsureInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append(value[i]);
                        break;
                }

                if (i > 0 && i % MaxLineLength == 0) {
                    //
                    // If current character is a high surrogate and the following 
                    // character is a low surrogate, don't break them. 
                    // Otherwise when we write the string to a file, we might lose 
                    // the characters.
                    // 
                    if( CharMethods.IsHighSurrogate(value[i])
                        && (i < value.Length -1) 
                        && CharMethods.IsLowSurrogate(value[i+1])){ 
                        b.Append(value[++i]);    
                    }
                                        
                    if (fInDoubleQuotes)
                        b.Append("\"");
                    fInDoubleQuotes = true;

                    b.Append("& _ ");
                    b.Append(Environment.NewLine);                    
                    b.Append(indentObj.IndentationString);
                    b.Append('\"');
                }
                ++i;
            }

            if (fInDoubleQuotes)
                b.Append("\"");

            return b.ToString();
        }

        //@TODO: Someday emit the hex version to be consistent with C#.
        private static void AppendEscapedChar(StringBuilder b, char value) {
            b.Append("&Global.Microsoft.VisualBasic.ChrW(");
            b.Append(((int)value).ToString(CultureInfo.InvariantCulture));
            b.Append(")");
        }

        protected override void OutputAttributeArgument(CodeAttributeArgument arg) {
            if (arg.Name != null && arg.Name.Length > 0) {
                OutputIdentifier(arg.Name);
                Output.Write(":=");
            }
            ((ICodeGenerator)this).GenerateCodeFromExpression(arg.Value, ((IndentedTextWriter)Output).InnerWriter, Options);
        }

        private void OutputAttributes(CodeAttributeDeclarationCollection attributes, bool inLine) {
            OutputAttributes(attributes, inLine, null, false);
        }

        private void OutputAttributes(CodeAttributeDeclarationCollection attributes, bool inLine, string prefix, bool closingLine) {
            if (attributes.Count == 0) return;
            IEnumerator en = attributes.GetEnumerator();
            bool firstAttr = true;
            GenerateAttributeDeclarationsStart(attributes);
            while (en.MoveNext()) {

                if (firstAttr) {
                    firstAttr = false;
                }
                else {
                    Output.Write(", ");
                    if (!inLine) {
                        ContinueOnNewLine("");
                        Output.Write(" ");
                    }
                }

                if (prefix != null && prefix.Length > 0) {
                    Output.Write(prefix);
                }

                CodeAttributeDeclaration current = (CodeAttributeDeclaration)en.Current;

                if( current.AttributeType != null) {                
                    Output.Write(GetTypeOutput(current.AttributeType));
                }
                Output.Write("(");

                bool firstArg = true;
                foreach (CodeAttributeArgument arg in current.Arguments) {
                    if (firstArg) {
                        firstArg = false;
                    }
                    else {
                        Output.Write(", ");
                    }

                    OutputAttributeArgument(arg);
                }

                Output.Write(")");

            }
            GenerateAttributeDeclarationsEnd(attributes);
            Output.Write(" ");            
            if (!inLine) {
                if (closingLine) {
                    Output.WriteLine();
                }
                else {
                    ContinueOnNewLine("");
                }
            }
        }

        protected override void OutputDirection(FieldDirection dir) {
            switch (dir) {
                case FieldDirection.In:
                    Output.Write("ByVal ");
                    break;
                case FieldDirection.Out:
                case FieldDirection.Ref:
                    Output.Write("ByRef ");
                    break;
            }
        }

        protected override void GenerateDefaultValueExpression(CodeDefaultValueExpression e) {
            Output.Write("CType(Nothing, " + GetTypeOutput(e.Type) + ")");
        }
        
        protected override void GenerateDirectionExpression(CodeDirectionExpression e) {
            // Visual Basic does not need to adorn the calling point with a direction, so just output the expression.
            GenerateExpression(e.Expression);
        }

        
        protected override void OutputFieldScopeModifier(MemberAttributes attributes) {
            switch (attributes & MemberAttributes.ScopeMask) {
                case MemberAttributes.Final:
                    Output.Write("");
                    break;
                case MemberAttributes.Static:
                    // ignore Static for fields in a Module since all fields in a module are already
                    //  static and it is a syntax error to explicitly mark them as static
                    //
                    if (!IsCurrentModule) {
                        Output.Write("Shared ");
                    }
                    break;
                case MemberAttributes.Const:
                    Output.Write("Const ");
                    break;
                default:
                    Output.Write("");
                    break;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based member
        ///       access modifier representation.
        ///    </para>
        /// </devdoc>
        protected override void OutputMemberAccessModifier(MemberAttributes attributes) {
            switch (attributes & MemberAttributes.AccessMask) {
                case MemberAttributes.Assembly:
                    Output.Write("Friend ");
                    break;
                case MemberAttributes.FamilyAndAssembly:
                    Output.Write("Friend ");
                    break;
                case MemberAttributes.Family:
                    Output.Write("Protected ");
                    break;
                case MemberAttributes.FamilyOrAssembly:
                    Output.Write("Protected Friend ");
                    break;
                case MemberAttributes.Private:
                    Output.Write("Private ");
                    break;
                case MemberAttributes.Public:
                    Output.Write("Public ");
                    break;
            }
        }

        private void OutputVTableModifier(MemberAttributes attributes) {
            switch (attributes & MemberAttributes.VTableMask) {
                case MemberAttributes.New:
                    Output.Write("Shadows ");
                    break;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based member scope modifier
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void OutputMemberScopeModifier(MemberAttributes attributes) {

            switch (attributes & MemberAttributes.ScopeMask) {
                case MemberAttributes.Abstract:
                    Output.Write("MustOverride ");
                    break;
                case MemberAttributes.Final:
                    Output.Write("");
                    break;
                case MemberAttributes.Static:
                    // ignore Static for members in a Module since all members in a module are already
                    //  static and it is a syntax error to explicitly mark them as static
                    //
                    if (!IsCurrentModule) {
                        Output.Write("Shared ");
                    }
                    break;
                case MemberAttributes.Override:
                    Output.Write("Overrides ");
                    break;
                case MemberAttributes.Private:
                    Output.Write("Private ");
                    break;
                default:
                    switch (attributes & MemberAttributes.AccessMask) {
                        case MemberAttributes.Family:
                        case MemberAttributes.Public:
                        case MemberAttributes.Assembly:                            
                            Output.Write("Overridable ");
                            break;
                        default:
                            // nothing;
                            break;
                    }
                    break;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based operator
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void OutputOperator(CodeBinaryOperatorType op) {
            switch (op) {
                case CodeBinaryOperatorType.IdentityInequality:
                    Output.Write("<>");
                    break;
                case CodeBinaryOperatorType.IdentityEquality:
                    Output.Write("Is");
                    break;
                case CodeBinaryOperatorType.BooleanOr:
                    Output.Write("OrElse");
                    break;
                case CodeBinaryOperatorType.BooleanAnd:
                    Output.Write("AndAlso");
                    break;
                case CodeBinaryOperatorType.ValueEquality:
                    Output.Write("=");
                    break;
                case CodeBinaryOperatorType.Modulus:
                    Output.Write("Mod");
                    break;
                case CodeBinaryOperatorType.BitwiseOr:
                    Output.Write("Or");
                    break;
                case CodeBinaryOperatorType.BitwiseAnd:
                    Output.Write("And");
                    break;
                default:
                    base.OutputOperator(op);
                break;
            }
        }

        private void GenerateNotIsNullExpression(CodeExpression e) {
            Output.Write("(Not (");
            GenerateExpression(e);
            Output.Write(") Is ");
            Output.Write(NullToken);
            Output.Write(")");
        }

        protected override void GenerateBinaryOperatorExpression(CodeBinaryOperatorExpression e) {
            if (e.Operator != CodeBinaryOperatorType.IdentityInequality) {
                base.GenerateBinaryOperatorExpression(e);
                return;
            }

            // "o <> nothing" should be "not o is nothing"
            if (e.Right is CodePrimitiveExpression && ((CodePrimitiveExpression)e.Right).Value == null){
                GenerateNotIsNullExpression(e.Left);
                return;    
            }
            if (e.Left is CodePrimitiveExpression && ((CodePrimitiveExpression)e.Left).Value == null){
                GenerateNotIsNullExpression(e.Right);
                return;
            }

            base.GenerateBinaryOperatorExpression(e);
        }

        protected override void OutputIdentifier(string ident) {
            Output.Write(CreateEscapedIdentifier(ident));
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based return type
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void OutputType(CodeTypeReference typeRef) {
            Output.Write(GetTypeOutputWithoutArrayPostFix(typeRef));
        }

        private void OutputTypeAttributes(CodeTypeDeclaration e) {
            if((e.Attributes & MemberAttributes.New) != 0) {
                Output.Write("Shadows ");
            }
            
            TypeAttributes attributes = e.TypeAttributes;

            if (e.IsPartial) {            
               Output.Write("Partial ");
            }

            switch(attributes & TypeAttributes.VisibilityMask) {
                case TypeAttributes.Public:                  
                case TypeAttributes.NestedPublic:
                    Output.Write("Public ");
                    break;
                case TypeAttributes.NestedPrivate:
                    Output.Write("Private ");
                    break;

                case TypeAttributes.NestedFamily:
                    Output.Write("Protected ");
                    break;
                case TypeAttributes.NotPublic:
                case TypeAttributes.NestedAssembly:
                case TypeAttributes.NestedFamANDAssem:
                    Output.Write("Friend ");
                    break;
                case TypeAttributes.NestedFamORAssem:
                    Output.Write("Protected Friend ");
                    break;
            }
            
            if (e.IsStruct) {
                Output.Write("Structure ");
            }
            else if (e.IsEnum) {
                Output.Write("Enum ");
            }     
            else {            

                switch (attributes & TypeAttributes.ClassSemanticsMask) {
                    case TypeAttributes.Class:
                        // if this "class" should generate as a module, then don't check
                        //  inheritance flags since modules can't inherit
                        if (IsCurrentModule) {
                            Output.Write("Module ");
                        }
                        else {
                            if ((attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed) {
                                Output.Write("NotInheritable ");
                            }
                            if ((attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract)  {
                                Output.Write("MustInherit ");
                            }
                            Output.Write("Class ");
                        }
                        break;                
                    case TypeAttributes.Interface:
                        Output.Write("Interface ");
                        break;
                }
            }
            
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based type name pair
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void OutputTypeNamePair(CodeTypeReference typeRef, string name) {
            if (string.IsNullOrEmpty(name)) 
                name = "__exception";
                
            OutputIdentifier(name);
            OutputArrayPostfix(typeRef);
            Output.Write(" As ");
            OutputType(typeRef);
        }

        private string GetArrayPostfix(CodeTypeReference typeRef) {
            string s = "";
            if (typeRef.ArrayElementType != null) {
                // Recurse up
                s = GetArrayPostfix(typeRef.ArrayElementType);
            }

            if (typeRef.ArrayRank > 0) {            
                char [] results = new char [typeRef.ArrayRank + 1];
                results[0] = '(';
                results[typeRef.ArrayRank] = ')';
                for (int i = 1; i < typeRef.ArrayRank; i++) {
                    results[i] = ',';
                }
                s = new string(results) + s;
            }

            return s;

        }

        private void OutputArrayPostfix(CodeTypeReference typeRef) {
            if (typeRef.ArrayRank > 0) {                        
                Output.Write(GetArrayPostfix(typeRef));
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based for loop statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateIterationStatement(CodeIterationStatement e) {
            GenerateStatement(e.InitStatement);
            Output.Write("Do While ");
            GenerateExpression(e.TestExpression);
            Output.WriteLine("");
            Indent++;
            GenerateVBStatements(e.Statements);
            GenerateStatement(e.IncrementStatement);
            Indent--;
            Output.WriteLine("Loop");
        }
        
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based primitive expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GeneratePrimitiveExpression(CodePrimitiveExpression e) {
            if (e.Value is char) {
                Output.Write("Global.Microsoft.VisualBasic.ChrW(" + Convert.ToInt16(e.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + ")");
            }
            else if (e.Value is SByte) {
                Output.Write("CSByte(");
                Output.Write(((SByte)e.Value).ToString(CultureInfo.InvariantCulture));
                Output.Write(")");
            }
            else if (e.Value is UInt16) {
                Output.Write(((UInt16)e.Value).ToString(CultureInfo.InvariantCulture));
                Output.Write("US");
            }
            else if (e.Value is UInt32) {
                Output.Write(((UInt32)e.Value).ToString(CultureInfo.InvariantCulture));
                Output.Write("UI");
            }
            else if (e.Value is UInt64) {
                Output.Write(((UInt64)e.Value).ToString(CultureInfo.InvariantCulture));
                Output.Write("UL");
            }            
            else {
                base.GeneratePrimitiveExpression(e);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based throw exception statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateThrowExceptionStatement(CodeThrowExceptionStatement e) {
            Output.Write("Throw");
            if (e.ToThrow != null) {
                Output.Write(" ");
                GenerateExpression(e.ToThrow);
            }
            Output.WriteLine("");
        }


        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based array creation expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateArrayCreateExpression(CodeArrayCreateExpression e) {
            Output.Write("New ");

            CodeExpressionCollection init = e.Initializers;
            if (init.Count > 0) {
                String typeName = GetTypeOutput(e.CreateType);
                Output.Write(typeName);
                
                // TODO: this is a hack to generate array if user doesn't
                // specify a array type. Should be removed sometime.
                if( typeName.IndexOf('(') == -1) { 
                    Output.Write("()");
                }

                Output.Write(" {");
                Indent++;
                OutputExpressionList(init);
                Indent--;
                Output.Write("}");
            }
            else {
                String typeName = GetTypeOutput(e.CreateType);
                
                int index = typeName.IndexOf('(');
                if( index == -1) {
                    // TODO: this is a hack to generate array if user doesn't
                    // specify a array type. Should be removed sometime.
                    Output.Write(typeName);
                    Output.Write('(');
                }
                else {
                    Output.Write(typeName.Substring(0, index+1));
                }

                // The tricky thing is we need to declare the size - 1
                if (e.SizeExpression != null) {
                    Output.Write("(");
                    GenerateExpression(e.SizeExpression);
                    Output.Write(") - 1");
                }
                else {
                    Output.Write(e.Size - 1);
                }

                if( index == -1) {
                    Output.Write(')');
                }
                else {
                    Output.Write(typeName.Substring(index+1));
                }

                Output.Write(" {}");
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based base reference expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateBaseReferenceExpression(CodeBaseReferenceExpression e) {
            Output.Write("MyBase");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based cast expression representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateCastExpression(CodeCastExpression e) {
            Output.Write("CType(");
            GenerateExpression(e.Expression);
            Output.Write(",");
            OutputType(e.TargetType);
            OutputArrayPostfix(e.TargetType);
            Output.Write(")");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based delegate creation expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateDelegateCreateExpression(CodeDelegateCreateExpression e) {
            Output.Write("AddressOf ");
            GenerateExpression(e.TargetObject);
            Output.Write(".");
            OutputIdentifier(e.MethodName);
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based field reference expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateFieldReferenceExpression(CodeFieldReferenceExpression e) {

            if (e.TargetObject != null) {
                GenerateExpression(e.TargetObject);
                Output.Write(".");
            }
            
            OutputIdentifier(e.FieldName);
        }

        protected override void GenerateSingleFloatValue(Single s) {
            if( float.IsNaN(s)) {
                Output.Write("Single.NaN");
            }
            else if( float.IsNegativeInfinity(s)) {
                Output.Write("Single.NegativeInfinity");                
            }
            else if( float.IsPositiveInfinity(s)) {
                Output.Write("Single.PositiveInfinity");                
            }
            else {
                Output.Write(s.ToString(CultureInfo.InvariantCulture));
                Output.Write('!');
            }
        }

        protected override void GenerateDoubleValue(double d) {
            if( double.IsNaN(d)) {
                Output.Write("Double.NaN");
            }
            else if( double.IsNegativeInfinity(d)) {
                Output.Write("Double.NegativeInfinity");                
            }
            else if( double.IsPositiveInfinity(d)) {
                Output.Write("Double.PositiveInfinity");                
            }
            else {
                Output.Write(d.ToString("R", CultureInfo.InvariantCulture));
                // always mark a double as being a double in case we have no decimal portion (e.g write 1D instead of 1 which is an int)
                Output.Write("R");
            }
        }


        protected override void GenerateArgumentReferenceExpression(CodeArgumentReferenceExpression e) {
            OutputIdentifier(e.ParameterName);
        }

        protected override void GenerateVariableReferenceExpression(CodeVariableReferenceExpression e) {
            OutputIdentifier(e.VariableName);
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based indexer expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateIndexerExpression(CodeIndexerExpression e) {
            GenerateExpression(e.TargetObject);
            // If this IndexerExpression is referencing to base, we need to emit
            // .Item after MyBase. Otherwise the code won't compile.
            if( e.TargetObject is CodeBaseReferenceExpression) {
                Output.Write(".Item");
            }

            Output.Write("(");
            bool first = true;
            foreach(CodeExpression exp in e.Indices) {            
                if (first) {
                    first = false;
                }
                else {
                    Output.Write(", ");
                }
                GenerateExpression(exp);
            }
            Output.Write(")");

        }

        protected override void GenerateArrayIndexerExpression(CodeArrayIndexerExpression e) {
            GenerateExpression(e.TargetObject);
            Output.Write("(");
            bool first = true;
            foreach(CodeExpression exp in e.Indices) {            
                if (first) {
                    first = false;
                }
                else {
                    Output.Write(", ");
                }
                GenerateExpression(exp);
            }
            Output.Write(")");

        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based code snippet expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateSnippetExpression(CodeSnippetExpression e) {
            Output.Write(e.Value);
        }
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based method invoke
        ///       expression.
        ///    </para>
        /// </devdoc>
        protected override void GenerateMethodInvokeExpression(CodeMethodInvokeExpression e) {
            GenerateMethodReferenceExpression(e.Method);
            CodeExpressionCollection parameters = e.Parameters;
            if (parameters.Count > 0) {
                Output.Write("(");
                OutputExpressionList(e.Parameters);
                Output.Write(")");
            }
        }

        protected override void GenerateMethodReferenceExpression(CodeMethodReferenceExpression e) {
            if (e.TargetObject != null) {
                GenerateExpression(e.TargetObject);
                Output.Write(".");
                Output.Write(e.MethodName);
            }
            else {
                OutputIdentifier(e.MethodName);
            }

            if( e.TypeArguments.Count > 0) {
                Output.Write(GetTypeArgumentsOutput(e.TypeArguments));
            }            
        }

        protected override void GenerateEventReferenceExpression(CodeEventReferenceExpression e) {
            if (e.TargetObject != null) {
                bool localReference = (e.TargetObject is CodeThisReferenceExpression);
                GenerateExpression(e.TargetObject);
                Output.Write(".");
                if (localReference) {
                    Output.Write(e.EventName + "Event");
                }
                else {
                    Output.Write(e.EventName);
                }
            }
            else {
                OutputIdentifier(e.EventName + "Event");
            }
        }

        private void GenerateFormalEventReferenceExpression(CodeEventReferenceExpression e) {
            if (e.TargetObject != null) {
                // Visual Basic Compiler does not like the me reference like this.
                if (!(e.TargetObject is CodeThisReferenceExpression)) {
                    GenerateExpression(e.TargetObject);
                    Output.Write(".");
                }
            }
            OutputIdentifier(e.EventName);
        }


        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based delegate invoke
        ///       expression.
        ///    </para>
        /// </devdoc>
        protected override void GenerateDelegateInvokeExpression(CodeDelegateInvokeExpression e) {
            if (e.TargetObject != null) {
                if (e.TargetObject is CodeEventReferenceExpression) {
                    Output.Write("RaiseEvent ");
                    GenerateFormalEventReferenceExpression((CodeEventReferenceExpression)e.TargetObject);
                }
                else {
                    GenerateExpression(e.TargetObject);
                }
            }

            CodeExpressionCollection parameters = e.Parameters;
            if (parameters.Count > 0) {
                Output.Write("(");
                OutputExpressionList(e.Parameters);
                Output.Write(")");
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based object creation
        ///       expression.
        ///    </para>
        /// </devdoc>
        protected override void GenerateObjectCreateExpression(CodeObjectCreateExpression e) {
            Output.Write("New ");
            OutputType(e.CreateType);
            // always write out the () to disambiguate cases like "New System.Random().Next(x,y)"
            Output.Write("(");
            OutputExpressionList(e.Parameters);
            Output.Write(")");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom
        ///       based parameter declaration expression representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateParameterDeclarationExpression(CodeParameterDeclarationExpression e) {
            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, true);
            }
            OutputDirection(e.Direction);
            OutputTypeNamePair(e.Type, e.Name);
        }

        protected override void GeneratePropertySetValueReferenceExpression(CodePropertySetValueReferenceExpression e) {
            Output.Write("value");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based this reference expression
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateThisReferenceExpression(CodeThisReferenceExpression e) {
            Output.Write("Me");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based method invoke statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateExpressionStatement(CodeExpressionStatement e) {
            GenerateExpression(e.Expression);
            Output.WriteLine("");
        }

        /// <devdoc>
        ///    <para>
        ///       Tells whether or not the given comment is a DocComment
        ///    </para>
        /// </devdoc>
        private bool IsDocComment(CodeCommentStatement comment) {
        
            return ((comment != null) && (comment.Comment != null) && comment.Comment.DocComment);
        }
        
        /// <include file='doc\VBCodeProvider.uex' path='docs/doc[@for="VBCodeGenerator.GenerateCommentStatements"]/*' />
        /// <devdoc>
        ///    <para>Overridden in order to output XML DocComments in the correct order for VB</para>
        /// </devdoc>
        protected override void GenerateCommentStatements(CodeCommentStatementCollection e) {

            // since the compiler emits a warning if XML DocComment blocks appear before
            //  normal comments, we need to output non-DocComments first, followed by
            //  DocComments.
            //            
            foreach (CodeCommentStatement comment in e) {
                if (!IsDocComment(comment)) {
                    GenerateCommentStatement(comment);
                }
            }

            foreach (CodeCommentStatement comment in e) {
                if (IsDocComment(comment)) {
                    GenerateCommentStatement(comment);
                }
            }
        }
        
        protected override void GenerateComment(CodeComment e) {
            String commentLineStart = e.DocComment? "'''": "'";            
            Output.Write(commentLineStart);                
            string value = e.Text;
            for (int i=0; i<value.Length; i++) {
                Output.Write(value[i]);

                if( value[i] == '\r') {
                    if (i < value.Length - 1 && value[i+1] == '\n') { // if next char is '\n', skip it
                        Output.Write('\n');
                        i++;
                    }
                    ((IndentedTextWriter) Output).InternalOutputTabs();
                    Output.Write(commentLineStart);
                }
                else if( value[i] == '\n') {
                    ((IndentedTextWriter) Output).InternalOutputTabs();
                    Output.Write(commentLineStart);
                }
                else if( value[i] == '\u2028' || value[i] == '\u2029' || value[i] == '\u0085') {
                    Output.Write(commentLineStart);
                }
            }
            Output.WriteLine();
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based method return statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateMethodReturnStatement(CodeMethodReturnStatement e) {
            if (e.Expression != null) {
                Output.Write("Return ");
                GenerateExpression(e.Expression);
                Output.WriteLine("");
            }
            else {
                Output.WriteLine("Return");
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based if statement representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateConditionStatement(CodeConditionStatement e) {
            Output.Write("If ");
            GenerateExpression(e.Condition);
            Output.WriteLine(" Then");
            Indent++;
            GenerateVBStatements(e.TrueStatements);
            Indent--;

            CodeStatementCollection falseStatemetns = e.FalseStatements;
            if (falseStatemetns.Count > 0) {
                Output.Write("Else");
                Output.WriteLine("");
                Indent++;
                GenerateVBStatements(e.FalseStatements);
                Indent--;
            }
            Output.WriteLine("End If");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based try catch finally statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateTryCatchFinallyStatement(CodeTryCatchFinallyStatement e) {
            Output.WriteLine("Try ");
            Indent++;
            GenerateVBStatements(e.TryStatements);
            Indent--;
            CodeCatchClauseCollection catches = e.CatchClauses;
            if (catches.Count > 0) {
                IEnumerator en = catches.GetEnumerator();
                while (en.MoveNext()) {
                    CodeCatchClause current = (CodeCatchClause)en.Current;
                    Output.Write("Catch ");
                    OutputTypeNamePair(current.CatchExceptionType, current.LocalName);
                    Output.WriteLine("");
                    Indent++;
                    GenerateVBStatements(current.Statements);
                    Indent--;
                }
            }

            CodeStatementCollection finallyStatements = e.FinallyStatements;
            if (finallyStatements.Count > 0) {
                Output.WriteLine("Finally");
                Indent++;
                GenerateVBStatements(finallyStatements);
                Indent--;
            }
            Output.WriteLine("End Try");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based assignment statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateAssignStatement(CodeAssignStatement e) {
            GenerateExpression(e.Left);
            Output.Write(" = ");
            GenerateExpression(e.Right);
            Output.WriteLine("");
        }

        protected override void GenerateAttachEventStatement(CodeAttachEventStatement e) {
            Output.Write("AddHandler ");
            GenerateFormalEventReferenceExpression(e.Event);
            Output.Write(", ");
            GenerateExpression(e.Listener);
            Output.WriteLine("");
        }

        protected override void GenerateRemoveEventStatement(CodeRemoveEventStatement e) {
            Output.Write("RemoveHandler ");
            GenerateFormalEventReferenceExpression(e.Event);
            Output.Write(", ");
            GenerateExpression(e.Listener);
            Output.WriteLine("");
        }

        protected override void GenerateSnippetStatement(CodeSnippetStatement e) {
            Output.WriteLine(e.Value);
        }

        protected override void GenerateGotoStatement(CodeGotoStatement e) {
            Output.Write("goto ");
            Output.WriteLine(e.Label);
        }

        protected override void GenerateLabeledStatement(CodeLabeledStatement e) {
            Indent--;
            Output.Write(e.Label);
            Output.WriteLine(":");
            Indent++;
            if (e.Statement != null) {
                GenerateStatement(e.Statement);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom variable declaration statement
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateVariableDeclarationStatement(CodeVariableDeclarationStatement e) {
            bool doInit = true;
            
            Output.Write("Dim ");

            CodeTypeReference typeRef = e.Type;
            if (typeRef.ArrayRank == 1 && e.InitExpression != null) {
                CodeArrayCreateExpression eAsArrayCreate = e.InitExpression as CodeArrayCreateExpression;
                if (eAsArrayCreate != null && eAsArrayCreate.Initializers.Count == 0) {
                    doInit = false;
                    OutputIdentifier(e.Name);
                    Output.Write("(");

                    if (eAsArrayCreate.SizeExpression != null) {
                        Output.Write("(");
                        GenerateExpression(eAsArrayCreate.SizeExpression);
                        Output.Write(") - 1");
                    }
                    else {
                        Output.Write(eAsArrayCreate.Size - 1);
                    }

                    Output.Write(")");

                    if (typeRef.ArrayElementType != null)
                        OutputArrayPostfix(typeRef.ArrayElementType); 
                        
                    Output.Write(" As ");
                    OutputType(typeRef);
                }
                else
                    OutputTypeNamePair(e.Type, e.Name);
            }
            else
                OutputTypeNamePair(e.Type, e.Name);
                
            if (doInit && e.InitExpression != null) {
                Output.Write(" = ");
                GenerateExpression(e.InitExpression);
            }   
                
            Output.WriteLine("");
        }
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based line pragma start
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateLinePragmaStart(CodeLinePragma e) {
            Output.WriteLine("");
            Output.Write("#ExternalSource(\"");
            Output.Write(e.FileName);
            Output.Write("\",");
            Output.Write(e.LineNumber);
            Output.WriteLine(")");
        }
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based line pragma end
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateLinePragmaEnd(CodeLinePragma e) {
            Output.WriteLine("");
            Output.WriteLine("#End ExternalSource");
        }


        protected override void GenerateEvent(CodeMemberEvent e, CodeTypeDeclaration c) {
            if (IsCurrentDelegate || IsCurrentEnum) return;

            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }
            
            string eventName = e.Name;
            if (e.PrivateImplementationType != null)
            {
                string impl = GetBaseTypeOutput(e.PrivateImplementationType);
                impl = impl.Replace('.', '_');
                e.Name = impl + "_" + e.Name;
            }

            OutputMemberAccessModifier(e.Attributes);
            Output.Write("Event ");
            OutputTypeNamePair(e.Type, e.Name);

            if (e.ImplementationTypes.Count > 0) {
                Output.Write(" Implements ");
                bool first = true;
                foreach (CodeTypeReference type in e.ImplementationTypes) {
                    if (first) {
                        first = false;
                    }   
                    else {
                        Output.Write(" , ");
                    }
                    OutputType(type);
                    Output.Write(".");
                    OutputIdentifier(eventName);
                }
            }
            else if (e.PrivateImplementationType != null) {
                Output.Write(" Implements ");
                OutputType(e.PrivateImplementationType);
                Output.Write(".");
                OutputIdentifier(eventName);
            }

            Output.WriteLine("");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based member
        ///       field representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateField(CodeMemberField e) {
            if (IsCurrentDelegate || IsCurrentInterface) return;

            if (IsCurrentEnum) {
                if (e.CustomAttributes.Count > 0) {
                    OutputAttributes(e.CustomAttributes, false);
                }

                OutputIdentifier(e.Name);
                if (e.InitExpression != null) {
                    Output.Write(" = ");
                    GenerateExpression(e.InitExpression);
                }
                Output.WriteLine("");
            }
            else {
                if (e.CustomAttributes.Count > 0) {
                    OutputAttributes(e.CustomAttributes, false);
                }

                OutputMemberAccessModifier(e.Attributes);
                OutputVTableModifier(e.Attributes);
                OutputFieldScopeModifier(e.Attributes);

                if (GetUserData(e, "WithEvents", false)) {
                    Output.Write("WithEvents ");
                }

                OutputTypeNamePair(e.Type, e.Name);
                if (e.InitExpression != null) {
                    Output.Write(" = ");
                    GenerateExpression(e.InitExpression);
                }
                Output.WriteLine("");
            }
        }

        private bool MethodIsOverloaded(CodeMemberMethod e, CodeTypeDeclaration c) {
            if ((e.Attributes & MemberAttributes.Overloaded) != 0) {
                return true;
            }
            IEnumerator en = c.Members.GetEnumerator();
            while (en.MoveNext()) {
                if (!(en.Current is CodeMemberMethod))
                    continue;
                CodeMemberMethod meth = (CodeMemberMethod) en.Current;

                if (!(en.Current is CodeTypeConstructor)
                    && !(en.Current is CodeConstructor)
                    && meth != e
                    && meth.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase)
                    && meth.PrivateImplementationType == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for
        ///       the specified CodeDom based snippet member representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateSnippetMember(CodeSnippetTypeMember e) {
            Output.Write(e.Text);
        }

        protected override void GenerateMethod(CodeMemberMethod e, CodeTypeDeclaration c) {
            if (!(IsCurrentClass || IsCurrentStruct || IsCurrentInterface)) return;

            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }

            // need to change the implements name before doing overloads resolution
            //
            string methodName = e.Name;
            if (e.PrivateImplementationType != null) {
                string impl = GetBaseTypeOutput(e.PrivateImplementationType);
                impl = impl.Replace('.', '_');
                e.Name = impl + "_" + e.Name;
            }

            if (!IsCurrentInterface) {
                if (e.PrivateImplementationType == null) {
                    OutputMemberAccessModifier(e.Attributes);
                    if (MethodIsOverloaded(e, c))
                        Output.Write("Overloads ");
                }
                OutputVTableModifier(e.Attributes);
                OutputMemberScopeModifier(e.Attributes);
            }
            else {
                // interface may still need "Shadows"
                OutputVTableModifier(e.Attributes);
            }
            bool sub = false;
            if (e.ReturnType.BaseType.Length == 0 || string.Compare(e.ReturnType.BaseType, typeof(void).FullName, StringComparison.OrdinalIgnoreCase) == 0) {
                sub = true;
            }

            if (sub) {
                Output.Write("Sub ");
            }
            else {
                Output.Write("Function ");
            }


            OutputIdentifier(e.Name);
            OutputTypeParameters(e.TypeParameters);
            
            Output.Write("(");
            OutputParameters(e.Parameters);
            Output.Write(")");

            if (!sub) {
                Output.Write(" As ");
                if (e.ReturnTypeCustomAttributes.Count > 0) {
                    OutputAttributes(e.ReturnTypeCustomAttributes, true);
                }

                OutputType(e.ReturnType);
                OutputArrayPostfix(e.ReturnType);
            }
            if (e.ImplementationTypes.Count > 0) {
                Output.Write(" Implements ");
                bool first = true;
                foreach (CodeTypeReference type in e.ImplementationTypes) {
                    if (first) {
                        first = false;
                    }   
                    else {
                        Output.Write(" , ");
                    }
                    OutputType(type);
                    Output.Write(".");
                    OutputIdentifier(methodName);
                }
            }
            else if (e.PrivateImplementationType != null) {
                Output.Write(" Implements ");
                OutputType(e.PrivateImplementationType);
                Output.Write(".");
                OutputIdentifier(methodName);
            }
            Output.WriteLine("");
            if (!IsCurrentInterface
                && (e.Attributes & MemberAttributes.ScopeMask) != MemberAttributes.Abstract) {
                Indent++;

                GenerateVBStatements(e.Statements);

                Indent--;
                if (sub) {
                    Output.WriteLine("End Sub");
                }
                else {
                    Output.WriteLine("End Function");
                }
            }
            // reset the name that possibly got changed with the implements clause
            e.Name = methodName;
        }

        protected override void GenerateEntryPointMethod(CodeEntryPointMethod e, CodeTypeDeclaration c) {
            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }

            Output.WriteLine("Public Shared Sub Main()");
            Indent++;

            GenerateVBStatements(e.Statements);

            Indent--;
            Output.WriteLine("End Sub");
        }

        private bool PropertyIsOverloaded(CodeMemberProperty e, CodeTypeDeclaration c) {
            if ((e.Attributes & MemberAttributes.Overloaded) != 0) {
                return true;
            }
            IEnumerator en = c.Members.GetEnumerator();
            while (en.MoveNext()) {
                if (!(en.Current is CodeMemberProperty))
                    continue;
                CodeMemberProperty prop = (CodeMemberProperty) en.Current;
                if ( prop != e
                    && prop.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase)
                    && prop.PrivateImplementationType == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based member property
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateProperty(CodeMemberProperty e, CodeTypeDeclaration c) {
            if (!(IsCurrentClass || IsCurrentStruct || IsCurrentInterface)) return;

            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }

            string propName = e.Name;
            if (e.PrivateImplementationType != null)
            {
                string impl = GetBaseTypeOutput(e.PrivateImplementationType);
                impl = impl.Replace('.', '_');
                e.Name = impl + "_" + e.Name;
            }
            if (!IsCurrentInterface) {
                if (e.PrivateImplementationType == null) {
                    OutputMemberAccessModifier(e.Attributes);
                    if (PropertyIsOverloaded(e,c)) {
                        Output.Write("Overloads ");
                    }
                }
                OutputVTableModifier(e.Attributes);
                OutputMemberScopeModifier(e.Attributes);
            }
            else {
                // interface may still need "Shadows"
                OutputVTableModifier(e.Attributes);
            }
            if (e.Parameters.Count > 0 && String.Compare(e.Name, "Item", StringComparison.OrdinalIgnoreCase) == 0) {
                Output.Write("Default ");
            }
            if (e.HasGet) {
                if (!e.HasSet) {
                    Output.Write("ReadOnly ");
                }
            }
            else if (e.HasSet) {
                Output.Write("WriteOnly ");
            }
            Output.Write("Property ");
            OutputIdentifier(e.Name);
            Output.Write("(");
            if (e.Parameters.Count > 0) {
                OutputParameters(e.Parameters);
            }
            Output.Write(")");
            Output.Write(" As ");
            OutputType(e.Type);
            OutputArrayPostfix(e.Type);

            if (e.ImplementationTypes.Count > 0) {
                Output.Write(" Implements ");
                bool first = true;
                foreach (CodeTypeReference type in e.ImplementationTypes) {
                    if (first) {
                        first = false;
                    }   
                    else {
                        Output.Write(" , ");
                    }
                    OutputType(type);
                    Output.Write(".");
                    OutputIdentifier(propName);
                }
            }
            else if (e.PrivateImplementationType != null) {
                Output.Write(" Implements ");
                OutputType(e.PrivateImplementationType);
                Output.Write(".");
                OutputIdentifier(propName);
            }

            Output.WriteLine("");

            if (!c.IsInterface && (e.Attributes & MemberAttributes.ScopeMask) != MemberAttributes.Abstract) {
                Indent++;

                if (e.HasGet) {

                    Output.WriteLine("Get");
                    if (!IsCurrentInterface) {
                        Indent++;

                        GenerateVBStatements(e.GetStatements);
                        e.Name = propName;

                        Indent--;
                        Output.WriteLine("End Get");
                    }
                }
                if (e.HasSet) {
                    Output.WriteLine("Set");
                    if (!IsCurrentInterface) {
                        Indent++;
                        GenerateVBStatements(e.SetStatements);
                        Indent--;
                        Output.WriteLine("End Set");
                    }
                }
                Indent--;
                Output.WriteLine("End Property");
            }

            e.Name = propName;
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based property reference
        ///       expression representation.
        ///    </para>
        /// </devdoc>
        protected override void GeneratePropertyReferenceExpression(CodePropertyReferenceExpression e) {

            if (e.TargetObject != null) {
                GenerateExpression(e.TargetObject);
                Output.Write(".");
                Output.Write(e.PropertyName);
            }
            else {
                OutputIdentifier(e.PropertyName);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based constructor
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateConstructor(CodeConstructor e, CodeTypeDeclaration c) {
            if (!(IsCurrentClass || IsCurrentStruct)) return;

            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }

            OutputMemberAccessModifier(e.Attributes);
            Output.Write("Sub New(");
            OutputParameters(e.Parameters);
            Output.WriteLine(")");
            Indent++;

            CodeExpressionCollection baseArgs = e.BaseConstructorArgs;
            CodeExpressionCollection thisArgs = e.ChainedConstructorArgs;

            if (thisArgs.Count > 0) {
                Output.Write("Me.New(");
                OutputExpressionList(thisArgs);
                Output.Write(")");
                Output.WriteLine("");                
            }
            else if (baseArgs.Count > 0) {
                Output.Write("MyBase.New(");
                OutputExpressionList(baseArgs);
                Output.Write(")");
                Output.WriteLine("");
            }
            else if(IsCurrentClass) {
                // struct doesn't have MyBase
                Output.WriteLine("MyBase.New");
            }

            GenerateVBStatements(e.Statements);
            Indent--;
            Output.WriteLine("End Sub");
        }
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based class constructor
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateTypeConstructor(CodeTypeConstructor e) {
            if (!(IsCurrentClass || IsCurrentStruct)) return;

            if (e.CustomAttributes.Count > 0) {
                OutputAttributes(e.CustomAttributes, false);
            }

            Output.WriteLine("Shared Sub New()");
            Indent++;
            GenerateVBStatements(e.Statements);
            Indent--;
            Output.WriteLine("End Sub");
        }

        protected override void GenerateTypeOfExpression(CodeTypeOfExpression e) {
            Output.Write("GetType(");
            Output.Write(GetTypeOutput(e.Type));            
            Output.Write(")");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the CodeDom based class start representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateTypeStart(CodeTypeDeclaration e) {
            if (IsCurrentDelegate) {
                if (e.CustomAttributes.Count > 0) {
                    OutputAttributes(e.CustomAttributes, false);
                }

                switch (e.TypeAttributes & TypeAttributes.VisibilityMask) {
                    case TypeAttributes.Public:
                        Output.Write("Public ");
                        break;
                    case TypeAttributes.NotPublic:
                    default:
                        break;
                }

                CodeTypeDelegate del = (CodeTypeDelegate)e;
                if (del.ReturnType.BaseType.Length > 0 && string.Compare(del.ReturnType.BaseType, "System.Void", StringComparison.OrdinalIgnoreCase) != 0)
                    Output.Write("Delegate Function ");
                else
                    Output.Write("Delegate Sub ");
                OutputIdentifier(e.Name);
                Output.Write("(");
                OutputParameters(del.Parameters);
                Output.Write(")");
                if (del.ReturnType.BaseType.Length > 0 && string.Compare(del.ReturnType.BaseType, "System.Void", StringComparison.OrdinalIgnoreCase) != 0) {
                    Output.Write(" As ");
                    OutputType(del.ReturnType);
                    OutputArrayPostfix(del.ReturnType);
                }
                Output.WriteLine("");
            }
            else if (e.IsEnum) {
                if (e.CustomAttributes.Count > 0) {
                    OutputAttributes(e.CustomAttributes, false);
                }
                OutputTypeAttributes(e);                                
                                
                OutputIdentifier(e.Name);

                if (e.BaseTypes.Count > 0) {
                    Output.Write(" As ");                    
                    OutputType(e.BaseTypes[0]);
                }

                Output.WriteLine("");
                Indent++;
            }
            else {                
                if (e.CustomAttributes.Count > 0) {
                    OutputAttributes(e.CustomAttributes, false);
                }
                OutputTypeAttributes(e);                                
                                
                OutputIdentifier(e.Name);
                OutputTypeParameters(e.TypeParameters);

                bool writtenInherits = false;
                bool writtenImplements = false;
                // For a structure we can't have an inherits clause
                if (e.IsStruct) {
                    writtenInherits = true;
                }
                // For an interface we can't have an implements clause
                if (e.IsInterface) {
                    writtenImplements = true;
                }
                Indent++;
                foreach (CodeTypeReference typeRef in e.BaseTypes) {
                    // if we're generating an interface, we always want to use Inherits because interfaces can't Implement anything. 
                    if (!writtenInherits && (e.IsInterface || !typeRef.IsInterface)) {
                        Output.WriteLine("");
                        Output.Write("Inherits ");
                        writtenInherits = true;
                    }
                    else if (!writtenImplements) {
                        Output.WriteLine("");
                        Output.Write("Implements ");
                        writtenImplements = true;
                    }
                    else {
                        Output.Write(", ");
                    }                 
                    OutputType(typeRef);
                }

                Output.WriteLine("");
            }
        }

        private void OutputTypeParameters(CodeTypeParameterCollection typeParameters) {            
            if( typeParameters.Count == 0) {
                return;
            }

            Output.Write("(Of ");
            bool first = true;
            for(int i = 0; i < typeParameters.Count; i++) {
                if( first) {
                    first = false;
                }
                else {
                    Output.Write(", ");
                }
                Output.Write(typeParameters[i].Name);
                OutputTypeParameterConstraints(typeParameters[i]);                
            }

            Output.Write(')');
        }

        // In VB, constraints are put right after the type paramater name.
        // In C#, there is a seperate "where" statement
        private void OutputTypeParameterConstraints(CodeTypeParameter typeParameter) {
            CodeTypeReferenceCollection constraints = typeParameter.Constraints;
            int constraintCount = constraints.Count;
            if( typeParameter.HasConstructorConstraint) {
                constraintCount++;
            }          

            if( constraintCount == 0) {
                return;
            }

            // generating something like: "ValType As {IComparable, Customer, New}"
            Output.Write(" As ");
            if(constraintCount > 1) {
                Output.Write(" {");
            }

            bool first = true;
            foreach (CodeTypeReference typeRef in constraints) {
                if (first) {
                    first = false;
                }
                else {
                    Output.Write(", ");
                }                 
                Output.Write(GetTypeOutput(typeRef));
            }
                
            if( typeParameter.HasConstructorConstraint) {
                if (!first) {
                    Output.Write(", ");
                }                 

                Output.Write("New");
            }
            
            if(constraintCount > 1) {
                Output.Write('}');
            }

        }
        
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based class end
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateTypeEnd(CodeTypeDeclaration e) {
            if (!IsCurrentDelegate) {
                Indent--;
                string ending;
                if (e.IsEnum) {
                    ending = "End Enum";
                }
                else if (e.IsInterface) {
                    ending = "End Interface";
                }
                else if (e.IsStruct) {
                    ending = "End Structure";
                } else {
                    if (IsCurrentModule) {
                        ending = "End Module";
                    }
                    else {
                        ending = "End Class";
                    }
                }
                Output.WriteLine(ending);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the CodeDom based namespace representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateNamespace(CodeNamespace e) {

            if (GetUserData(e, "GenerateImports", true)) {
                GenerateNamespaceImports(e);
            }
            Output.WriteLine();
            GenerateCommentStatements(e.Comments);
            GenerateNamespaceStart(e);
            GenerateTypes(e);
            GenerateNamespaceEnd(e);
        }

        protected bool AllowLateBound(CodeCompileUnit e) {
            object o = e.UserData["AllowLateBound"];
            if (o != null && o is bool) {
                return (bool)o;
            }
            // We have Option Strict Off by default because it can fail on simple things like dividing
            // two integers.
            return true;
        }

        protected bool RequireVariableDeclaration(CodeCompileUnit e) {
            object o = e.UserData["RequireVariableDeclaration"];
            if (o != null && o is bool) {
                return (bool)o;
            }
            return true;
        }

        private bool GetUserData(CodeObject e, string property, bool defaultValue) {
            object o = e.UserData[property];
            if (o != null && o is bool) {
                return (bool)o;
            }
            return defaultValue;
        }

        protected override void GenerateCompileUnitStart(CodeCompileUnit e) {
            base.GenerateCompileUnitStart(e);
        
            Output.WriteLine("'------------------------------------------------------------------------------");
            Output.Write("' <");
            Output.WriteLine(SR.GetString(SR.AutoGen_Comment_Line1));
            Output.Write("'     ");
            Output.WriteLine(SR.GetString(SR.AutoGen_Comment_Line2));
            Output.Write("'     ");
            Output.Write(SR.GetString(SR.AutoGen_Comment_Line3));
#if !WIN8
            Output.WriteLine(System.Environment.Version.ToString());
#endif
            Output.WriteLine("'");
            Output.Write("'     ");
            Output.WriteLine(SR.GetString(SR.AutoGen_Comment_Line4));
            Output.Write("'     ");
            Output.WriteLine(SR.GetString(SR.AutoGen_Comment_Line5));
            Output.Write("' </");
            Output.WriteLine(SR.GetString(SR.AutoGen_Comment_Line1));            
            Output.WriteLine("'------------------------------------------------------------------------------");
            Output.WriteLine("");

            if (AllowLateBound(e))
                Output.WriteLine("Option Strict Off");
            else
                Output.WriteLine("Option Strict On");

            if (!RequireVariableDeclaration(e))
                Output.WriteLine("Option Explicit Off");
            else
                Output.WriteLine("Option Explicit On");

            Output.WriteLine();

        }

        protected override void GenerateCompileUnit(CodeCompileUnit e) {
           
            GenerateCompileUnitStart(e);

            List<string> importList;            
            // Visual Basic needs all the imports together at the top of the compile unit.
            // If generating multiple namespaces, gather all the imports together
            importList = new List<string>();
            foreach (CodeNamespace nspace in e.Namespaces) {
                // mark the namespace to stop it generating its own import list
                nspace.UserData["GenerateImports"] = false;

                // Collect the unique list of imports
                foreach (CodeNamespaceImport import in nspace.Imports) {
                    if (!importList.Contains(import.Namespace)) {
                        importList.Add(import.Namespace);
                    }
                }
            }

            importList.Sort();
            // now output the imports
            foreach(string import in importList) {
                Output.Write("Imports ");
                OutputIdentifier(import);
                Output.WriteLine("");
            }

            if (e.AssemblyCustomAttributes.Count > 0) {
                OutputAttributes(e.AssemblyCustomAttributes, false, "Assembly: ", true);
            }

            GenerateNamespaces(e);
            GenerateCompileUnitEnd(e);            
        }
        
        protected override void GenerateDirectives(CodeDirectiveCollection directives) {
            for (int i = 0; i < directives.Count; i++) {
                CodeDirective directive = directives[i];
                if (directive is CodeChecksumPragma) {
                    GenerateChecksumPragma((CodeChecksumPragma)directive);
                }
                else if (directive is CodeRegionDirective) {
                    GenerateCodeRegionDirective((CodeRegionDirective)directive);
                }
            }
        }

        private void GenerateChecksumPragma(CodeChecksumPragma checksumPragma) {
            // the syntax is: #ExternalChecksum("FileName","GuidChecksum","ChecksumValue")
            Output.Write("#ExternalChecksum(\"");
            Output.Write(checksumPragma.FileName);
            Output.Write("\",\"");
            Output.Write(checksumPragma.ChecksumAlgorithmId.ToString("B", CultureInfo.InvariantCulture));
            Output.Write("\",\"");
            if (checksumPragma.ChecksumData != null) {
                foreach(Byte b in checksumPragma.ChecksumData) {
                    Output.Write(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            Output.WriteLine("\")");            
        }
                
        private void GenerateCodeRegionDirective(CodeRegionDirective regionDirective) {
            // VB does not support regions within statement blocks
            if (IsGeneratingStatements()) {
                return;
            }
            if (regionDirective.RegionMode == CodeRegionMode.Start) {
                Output.Write("#Region \"");
                Output.Write(regionDirective.RegionText);
                Output.WriteLine("\"");
            }
            else if (regionDirective.RegionMode == CodeRegionMode.End) {
                Output.WriteLine("#End Region");
            }
        }        

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based namespace representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateNamespaceStart(CodeNamespace e) {
            if (e.Name != null && e.Name.Length > 0) {
                Output.Write("Namespace ");
                string[] names = e.Name.Split('.');
                Debug.Assert( names.Length > 0);
                OutputIdentifier(names[0]);
                for( int i = 1; i< names.Length; i++) {
                    Output.Write(".");
                    OutputIdentifier(names[i]);                    
                }
                Output.WriteLine();
                Indent++;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based namespace representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateNamespaceEnd(CodeNamespace e) {
            if (e.Name != null && e.Name.Length > 0) {
                Indent--;
                Output.WriteLine("End Namespace");
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based namespace import
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateNamespaceImport(CodeNamespaceImport e) {
            Output.Write("Imports ");
            OutputIdentifier(e.Namespace);
            Output.WriteLine("");
        }

        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based attribute block start
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateAttributeDeclarationsStart(CodeAttributeDeclarationCollection attributes) {
            Output.Write("<");
        }
        /// <devdoc>
        ///    <para>
        ///       Generates code for the specified CodeDom based attribute block end
        ///       representation.
        ///    </para>
        /// </devdoc>
        protected override void GenerateAttributeDeclarationsEnd(CodeAttributeDeclarationCollection attributes) {
            Output.Write(">");
        }

        public static bool IsKeyword(string value) {
            return FixedStringLookup.Contains(keywords, value, true);
        }

        protected override bool Supports(GeneratorSupport support) {
            return ((support & LanguageSupport) == support);
        }

        /// <devdoc>
        ///    <para>
        ///       Gets whether the specified identifier is valid.
        ///    </para>
        /// </devdoc>
        protected override bool IsValidIdentifier(string value) {

            // identifiers must be 1 char or longer
            //
            if (value == null || value.Length == 0) {
                return false;
            }

            if (value.Length > 1023)
                return false;

            // identifiers cannot be a keyword unless surrounded by []'s
            //
            if (value[0] != '[' || value[value.Length - 1] != ']') {
                if (IsKeyword(value)) {
                    return false;
                }
            } else {
                value = value.Substring(1, value.Length - 2);
            }

            // just _ as an identifier is not valid. 
            if (value.Length == 1 && value[0] == '_')
                return false;
            
            return CodeGenerator.IsValidLanguageIndependentIdentifier(value);
        }

        protected override string CreateValidIdentifier(string name) {
            if (IsKeyword(name)) {
                return "_" + name;
            }
            return name;
        }

        protected override string CreateEscapedIdentifier(string name) {
            if (IsKeyword(name)) {
                return "[" + name + "]";
            }
            return name;
        }      
        
        private string GetBaseTypeOutput(CodeTypeReference typeRef) {
            string baseType = typeRef.BaseType;

            if (baseType.Length == 0) {
                return "Void";
            } 
            else if (string.Compare(baseType, "System.Byte", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Byte";
            }
            else if (string.Compare(baseType, "System.SByte", StringComparison.OrdinalIgnoreCase) == 0) {
                return "SByte";
            }
            else if (string.Compare(baseType, "System.Int16", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Short";
            }
            else if (string.Compare(baseType, "System.Int32", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Integer";
            }
            else if (string.Compare(baseType, "System.Int64", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Long";
            }
            else if (string.Compare(baseType, "System.UInt16", StringComparison.OrdinalIgnoreCase) == 0) {
                return "UShort";
            }
            else if (string.Compare(baseType, "System.UInt32", StringComparison.OrdinalIgnoreCase) == 0) {
                return "UInteger";
            }
            else if (string.Compare(baseType, "System.UInt64", StringComparison.OrdinalIgnoreCase) == 0) {
                return "ULong";
            }
            else if (string.Compare(baseType, "System.String", StringComparison.OrdinalIgnoreCase) == 0) {
                return "String";
            }
            else if (string.Compare(baseType, "System.DateTime", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Date";
            }
            else if (string.Compare(baseType, "System.Decimal", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Decimal";
            }
            else if (string.Compare(baseType, "System.Single", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Single";
            }
            else if (string.Compare(baseType, "System.Double", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Double";
            }
            else if (string.Compare(baseType, "System.Boolean", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Boolean";
            }
            else if (string.Compare(baseType, "System.Char", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Char";
            }
            else if (string.Compare(baseType, "System.Object", StringComparison.OrdinalIgnoreCase) == 0) {
                return "Object";
            }
            else {
                StringBuilder sb = new StringBuilder(baseType.Length + 10);
                if((typeRef.Options & CodeTypeReferenceOptions.GlobalReference) != 0) {
                    sb.Append("Global.");
                }

                int lastIndex = 0;
                int currentTypeArgStart = 0;
                for (int i=0; i<baseType.Length; i++) {
                    switch (baseType[i]) {
                    case '+':
                    case '.':
                        sb.Append(CreateEscapedIdentifier(baseType.Substring(lastIndex, i-lastIndex)));
                        sb.Append('.');
                        i++;
                        lastIndex = i;
                        break;
                        
                    case '`':
                        sb.Append(CreateEscapedIdentifier(baseType.Substring(lastIndex, i-lastIndex)));
                        i++;    // skip the '
                        int numTypeArgs = 0;
                        while (i < baseType.Length && baseType[i] >= '0' && baseType[i] <='9') {
                            numTypeArgs = numTypeArgs*10 + (baseType[i] - '0');
                            i++;
                        }
                
                        GetTypeArgumentsOutput(typeRef.TypeArguments, currentTypeArgStart, numTypeArgs, sb);
                        currentTypeArgStart += numTypeArgs;
                
                        // Arity can be in the middle of a nested type name, so we might have a . or + after it. 
                        // Skip it if so. 
                        if (i < baseType.Length &&  (baseType[i] =='+' || baseType[i] == '.')) {
                            sb.Append('.');
                            i++;
                        }
                            
                        lastIndex = i;
                        break;
                    }
                }
                
                if (lastIndex < baseType.Length)
                    sb.Append(CreateEscapedIdentifier(baseType.Substring(lastIndex)));

                return sb.ToString();                
            }
        }

        private string GetTypeOutputWithoutArrayPostFix(CodeTypeReference typeRef) {
            StringBuilder sb = new StringBuilder(); 

            while( typeRef.ArrayElementType != null) {
                typeRef = typeRef.ArrayElementType;
            }
            
            sb.Append(GetBaseTypeOutput(typeRef));
            return sb.ToString();
        }

        private String GetTypeArgumentsOutput(CodeTypeReferenceCollection typeArguments) {
            StringBuilder sb = new StringBuilder(128);
            GetTypeArgumentsOutput(typeArguments, 0, typeArguments.Count, sb);
            return sb.ToString();
        }
            

        private void GetTypeArgumentsOutput(CodeTypeReferenceCollection typeArguments, int start, int length, StringBuilder sb) {
            sb.Append("(Of ");
            bool first = true;
            for( int i = start; i < start+length; i++) {
                if( first) {
                    first = false;
                }
                else {
                    sb.Append(", ");
                }                

                // it's possible that we call GetTypeArgumentsOutput with an empty typeArguments collection.  This is the case
                // for open types, so we want to just output the brackets and commas. 
                if (i < typeArguments.Count)
                    sb.Append(GetTypeOutput(typeArguments[i]));                 
            }
            sb.Append(')');
        }

        protected override string GetTypeOutput(CodeTypeReference typeRef) {
            string s = string.Empty;            
            s += GetTypeOutputWithoutArrayPostFix(typeRef);
            
            if (typeRef.ArrayRank > 0) {
                s += GetArrayPostfix(typeRef);
            }
            return s;
        }

        protected override void ContinueOnNewLine(string st) {
            Output.Write(st);
            Output.WriteLine(" _");
        }
        
        private bool IsGeneratingStatements() {
            Debug.Assert(statementDepth >= 0, "statementDepth >= 0");
            return (statementDepth > 0);
        }
        
        private void GenerateVBStatements(CodeStatementCollection stms) {
            statementDepth++;
            try {
                GenerateStatements(stms);
            }
            finally {
                statementDepth--;
            }
        }
    }
}

