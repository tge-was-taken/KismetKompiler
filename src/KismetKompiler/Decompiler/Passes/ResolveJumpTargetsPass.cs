using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KismetKompiler.Decompiler.Context;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Decompiler.Passes
{

    public class ResolveJumpTargetsPass : IDecompilerPass
    {
        private int? ResolveComputedJump(DecompilerContext context, KismetExpression expr)
        {
            // TODO
            if (expr is EX_LocalVariable var)
            {
                var prop = context.Asset.GetProperty(var.Variable);
                if (prop is Export exp)
                {
                }
            }

            return null;
        }

        public Node Execute(DecompilerContext context, Node root)
        {
            foreach (var node in root.Children)
            {
                if (node is JumpNode jumpNode)
                {
                    switch (jumpNode.Source)
                    {
                        case EX_Jump expr:
                            jumpNode.Target = root.Children.First(x => x.CodeStartOffset == expr.CodeOffset);
                            break;
                        case EX_JumpIfNot expr:
                            jumpNode.Target = root.Children.First(x => x.CodeStartOffset == expr.CodeOffset);
                            break;
                        case EX_ComputedJump expr:
                            {
                                var codeOffset = ResolveComputedJump(context, expr.CodeOffsetExpression);
                                if (codeOffset != null)
                                {
                                    jumpNode.Target = root.Children.First(x => x.CodeStartOffset == codeOffset);
                                }
                            }
                            break;
                        case EX_SwitchValue expr:
                            {
                                jumpNode.Target = root.Children.FirstOrDefault(x => x.CodeStartOffset == expr.EndGotoOffset);
                            }
                            break;
                    }
                }
                else
                {
                    Execute(context, node);
                }
            }

            return root;
        }
    }
}
