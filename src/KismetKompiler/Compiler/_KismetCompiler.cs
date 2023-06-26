//using Antlr4.Runtime;
//using Antlr4.Runtime.Misc;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using UAssetAPI;
//using UAssetAPI.Kismet.Bytecode;
//using UAssetAPI.Kismet.Bytecode.Expressions;
//using UAssetAPI.UnrealTypes;

//namespace KismetKompiler
//{
//    public class KismetCompiler
//    {
//        private readonly UAsset asset;

//        public KismetCompiler(UAsset asset)
//        {
//            this.asset = asset;
//        }

//        public void Compile(string file)
//        {
//            var stream = new AntlrInputStream(File.OpenText(file));
//            var lexer = new KismetCLexer(stream);
//            var tokenStream = new CommonTokenStream(lexer);
//            var parser = new KismetCParser(tokenStream);
//            parser.BuildParseTree = true;
//            parser.ErrorHandler = new DefaultErrorStrategy();
//            parser.RemoveErrorListeners();
//            parser.AddErrorListener(new ConsoleAntlrErrorListener());
//            var compilationUnit = parser.compilationUnit();
//            //var visitor = new KismetCCompilerVisitor();
//            //visitor.Visit(compilationUnit);
//            var declarations = compilationUnit.declarationStatement();
//            foreach (var decl in declarations)
//            {
//                var proc = decl.procedureDeclarationStatement();
//                if (proc != null)
//                {
//                    foreach (var stmt in proc.compoundStatement().statement())
//                    {
//                        var label = stmt.declarationStatement()?.labelDeclarationStatement()?.Identifier()?.GetText();
//                        var call = stmt.expression() as CallExpressionContext;
//                        if (call != null)
//                        {
//                            var instr = ParseExpression(call);
//                        }
                            
//                    }
//                }

//            }
//        }

//        private KismetExpression ParseExpression(CallExpressionContext call)
//        {
//            var instr = call.Identifier().GetText();

//            switch (instr)
//            {
//                case "EX_ComputedJump":
//                    return new EX_ComputedJump() { CodeOffsetExpression = ParseExpression(call.argumentList().argument(0)) };
//                case "EX_LocalVariable":
//                    return new EX_LocalVariable()
//                    {
//                        Variable = new()
//                        {
//                            Old = GetPackageIndex(GetString(call.argumentList().argument(0)))
//                        }
//                    };
//                case "EX_CallMath":
//                    return new EX_CallMath()
//                    {
//                        StackNode = GetPackageIndex(GetString(call.argumentList().argument(0))),
//                        Parameters = call.argumentList().argument().Skip(1).Select(ParseExpression).ToArray()
//                    };
//                case "EX_UnicodeStringConst":
//                    return new EX_UnicodeStringConst()
//                    {
//                        Value = GetString(call.argumentList().argument(0))
//                    };
//                case "EX_Jump":
//                    return new EX_Jump()
//                    {
//                        CodeOffset = uint.Parse(GetIdentifier(call.argumentList().argument(0)).Substring(1))
//                    };
//                case "EX_LocalVirtualFunction":
//                    return new EX_LocalVirtualFunction()
//                    {
//                        VirtualFunctionName = new(asset, GetString(call.argumentList().argument(0))),
//                        Parameters = call.argumentList().argument().Skip(1).Select(ParseExpression).ToArray()
//                    };
//                //case "EX_Context":
//                //    return new EX_Context()
//                //    {
//                //        ContextExpression
//                //    }

//                default:
//                    throw new NotImplementedException();
//            }
//        }

//        private FPackageIndex GetPackageIndex(string name)
//        {
//            foreach (var import in asset.Imports)
//            {
//                if (import.ObjectName.Value.Value == name)
//                {
//                    return new FPackageIndex(-(asset.Imports.IndexOf(import) + 1));
//                }
//            }
//            foreach (var export in asset.Exports)
//            {
//                if (export.ObjectName.Value.Value == name)
//                {
//                    return new FPackageIndex(+(asset.Exports.IndexOf(export) + 1));
//                }
//            }
//            return null;
//        }

//        private KismetExpression ParseExpression(ArgumentContext arg)
//        {
//            var call = arg.expression() as CallExpressionContext;
//            Debug.Assert(call != null);
//            return ParseExpression(call);
//        }

//        private string GetIdentifier(ArgumentContext arg)
//        {
//            var primaryExpression = (arg.expression() as PrimaryExpressionContext);
//            var identifierExpressionContext = primaryExpression.primary() as IdentifierExpressionContext;
//            return identifierExpressionContext.Identifier().GetText();
//        }


//        private string GetString(ArgumentContext arg)
//        {
//            var primaryExpression = (arg.expression() as PrimaryExpressionContext);
//            var constantExpression = primaryExpression.primary() as ConstantExpressionContext;
//            return constantExpression.constant().StringLiteral().GetText().Trim('"');
//        }

//        private int GetInt(ArgumentContext arg)
//        {
//            var primaryExpression = (arg.expression() as PrimaryExpressionContext);
//            var constantExpression = primaryExpression.primary() as ConstantExpressionContext;
//            return int.Parse(constantExpression.constant().IntLiteral().GetText());
//        }

//        private string GetVariable(ArgumentContext arg)
//        {
//            return "";
//        }
//    }

//    //public class KismetCCompilerVisitor : KismetCBaseVisitor<object>
//    //{
//    //    public override object VisitProcedureDeclarationStatement([NotNull] KismetCParser.ProcedureDeclarationStatementContext context)
//    //    {
//    //        var name = context.Identifier().GetText();


//    //        return base.VisitProcedureDeclarationStatement(context);
//    //    }

//    //    //public override object VisitFunctionDefinition([NotNull] KismetCParser.FunctionDefinitionContext context)
//    //    //{
//    //    //    var name = context.declarator().directDeclarator().directDeclarator().GetText();

//    //    //    return base.VisitFunctionDefinition(context);
//    //    //}
//    //}
//}
