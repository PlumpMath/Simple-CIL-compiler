﻿namespace Language
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;

    using AST;

    using Semantic;
    using Semantic.ASTVisitor;
    using Semantic.Data;

    using TinyPG;

    public class SourceCodeHandler
    {
        public SourceCodeHandler(string source, bool generateAssembly)
        {
            var scanner = new Scanner();
            var parser = new Parser(scanner);

            //var highlighter = new TextHighlighter(source, scanner, parser);

            var tree = parser.Parse(source, "", new AstGenerationTree());
            PopulateSyntaxTree(tree.Nodes.First(), ParseTree);
            PopulateTokens(scanner);

            if (!tree.Errors.Any())
            {
                try
                {
                    tree.Eval();
                    var r = Namespaces.Root;
                    var p = AstCreator.GetProgram();
                    p.Optimize();
                    ASTTree = p.GetNodes();
                    //new ControlFlowVisitor().Visit(p);
                    PopulateIdentifierTree(Namespaces.Root, IdentifierTree);
                    if (generateAssembly)
                    {
                        var codeGeneration = new CodeGenVisitor(p);
                        try
                        {
                            codeGeneration.GenerateAssembly($"{AppDomain.CurrentDomain.BaseDirectory}\\output.exe");
                        }
                        catch (Exception exception)
                        {
                            tree.Errors.Add(new ParseError($"Что-то пошло не так {exception.Message}", 0));
                        }
                    }
                }
                catch (ParseException parseException)
                {
                    tree.Errors.Add(parseException.Error);
                }
                
            }

            Errors = tree.Errors;

            Status = $"Errors: {string.Join(Environment.NewLine, tree.Errors.Select(e => e.ToString()))}{Environment.NewLine}";
        }

        private void PopulateTokens(Scanner scanner)
        {
            TokenType[] tokenWithTexts =
            {
                TokenType.STRING, TokenType.BOOL, TokenType.COMMENT, TokenType.COMP,
                TokenType.DOUBLE, TokenType.INTEGER, TokenType.IDENTIFIER, TokenType.OPER
            };
            Tokens.AddRange(
                scanner.RecognizedTokens.Select(
                    t =>
                        {
                            var result = $"{t.Type.ToString()} ({t.Line}:{t.Column})";
                            if (tokenWithTexts.Contains(t.Type))
                            {
                                result += $" : {t.Text}";
                            }
                            return result;
                        }));
        }

        public ParseErrors Errors { get; set; }

        public List<string> Tokens { get; private set; } = new List<string>();

        public TreeNode ParseTree { get; private set; } = new TreeNode();

        public TreeNode IdentifierTree { get; set; } = new TreeNode();

        public TreeNode ASTTree { get; set; }

        public string Status { get; private set; }

        private void PopulateIdentifierTree(Namespace namespc, TreeNode node)
        {
            var nodeText = namespc.Name;
            var singleNode = new TreeNode(nodeText) { Tag = namespc };

            foreach (var literal in namespc.SymbolsSameLevel)
            {
                var literalNode = new TreeNode($"{literal.Type} : {literal.Name}");
                if (literal.Type == SymbolType.Array)
                {
                    literalNode = new TreeNode($"{literal.ArrayValueType}[{literal.ArrayKeyType}] : {literal.Name}");
                }
                singleNode.Nodes.Add(literalNode);
            }

            
            node.Nodes.Add(singleNode);

            //            foreach (var child in namespc.Children.Where(x => x.Children.Any()))
            foreach (var child in namespc.Children)
            {
                PopulateIdentifierTree(child, singleNode);
            }
            
        }

        private void PopulateSyntaxTree(ParseNode node, TreeNode treeNode)
        {
            var nodeText = node.Text;
            if (node.Token.Line != 0 && node.Token.Column != 0)
            {
                nodeText += $"({node.Token.Line}:{node.Token.Column})";
            }
            var singleNode = new TreeNode(nodeText) { Tag = node };
            treeNode.Nodes.Add(singleNode);
            foreach (var parseNode in node.Nodes)
            {
                PopulateSyntaxTree(parseNode, singleNode);
            }
        }

        private void ToAST(ref ParseNode tree)
        {
            if (tree.Nodes.Count > 1)
            {
                for (var i = 0; i < tree.Nodes.Count; i++)
                {
                    var parseNode = tree.Nodes[i];
                    ToAST(ref parseNode);
                }
            }
            else if (tree.Nodes.Count == 1)
            {
                tree.Nodes = tree.Nodes.First().Nodes;
                ToAST(ref tree);
            }
        }
    }
}
