﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Castle.Core.Internal;
using GraphLabs.CommonUI.Controls.ViewModels;
using GraphLabs.Graphs;

namespace GraphLabs.Tasks.Template
{
    public partial class TaskTemplateViewModel
    {
        #region Полезности

        private const string ImageResourcesPath = @"/GraphLabs.Tasks.Template;component/Images/";

        private Uri GetImageUri(string imageFileName)
        {
            return new Uri(ImageResourcesPath + imageFileName, UriKind.Relative);
        }

        private static UndirectedGraph CombUndGraph(IGraph g, int n)
        {
            var graph = (UndirectedGraph)((UndirectedGraph)g).Clone();
            var list = new List<String>();
            var order = new Stack<int>();
            g.Vertices.ForEach(v => list.Add(v.Name));
            for (var i = 1; i <= graph.VerticesCount; i++)
            {
                order.Push(n % i);
                n /= i;
            }
            for (var i = 0; i < graph.VerticesCount; i++)
            {
                var j = order.Pop();
                graph.Vertices[i].Rename(list[j]);
                list.RemoveAt(j);
            }
            return graph;
        }

        private static int F(int n)
        {
            return n > 1 ? F(n - 1)*n : 1;
        }

        private static void FindAllSubgraphs(IGraph graph, int iStart, List<Graphs.IVertex> cStart, ICollection<IGraph> collection)
        {
            for (var i = iStart; i < cStart.Count; i++)
            {
                var c = new List<Graphs.IVertex>();
                cStart.ForEach(v => c.Add(new Graphs.Vertex(v.Name)));
                c.RemoveAt(i);

                FindAllSubgraphs(graph, i, c, collection);

                var g = new UndirectedGraph();
                c.ForEach(v => g.AddVertex(new Graphs.Vertex(v.Name)));
                g.Vertices.ForEach(v1 =>
                        g.Vertices.ForEach(v2 =>
                        {
                            if (graph[graph.Vertices.Single(v => v.Name == v1.Name),
                                      graph.Vertices.Single(v => v.Name == v2.Name)] != null && g[v1, v2] == null)
                                g.AddEdge(new UndirectedEdge(v1, v2));
                        }));
                if (GraphLib.Lib.SingleOrDefault(s => s.Equals(g)) == null && Unique(g, collection) &&
                    g.VerticesCount > 0)
                {
                    g.Vertices.ForEach(v => v.Rename(v.Name + '.'));
                    for (var j = 0; j < g.VerticesCount; j++)
                        g.Vertices[j].Rename(j.ToString());
                    collection.Add(g);
                }
            }
        }

        private static bool Unique(IGraph g, IEnumerable<IGraph> c)
        {
            var unique = true;

            var gCopy = UndirectedGraph.CreateEmpty(g.VerticesCount);
            for (var i = 0; i < g.VerticesCount; i++)
                for (var j = i + 1; j < g.VerticesCount; j++)
                    if (g[g.Vertices[i], g.Vertices[j]] != null)
                        gCopy.AddEdge(new UndirectedEdge(gCopy.Vertices[i], gCopy.Vertices[j]));

            var graphs = new ObservableCollection<IGraph>();
            var n = F(gCopy.VerticesCount);
            for (var i = 0; i < n; i++)
                graphs.Add(CombUndGraph(gCopy, i));
            graphs.ForEach(g1 =>
                c.ForEach(g2 =>
                    unique &= !g1.Equals(g2)
                    )
                );

            return unique;
        }

        #endregion

        private void InitToolBarCommands()
        {
            #region Первый этап
            #region Добавление рёбер
            var phase1AddEdgeCommand = new ToolBarToggleCommand(
                () =>
                {
                    IsMouseVerticesMovingEnabled = false;
                    IsEgesAddingEnabled = true;
                    _state = State.EdgesAdding;
                    UserActionsManager.RegisterInfo(Strings.Strings_RU.buttonEdgesOn);
                },
                () =>
                {
                    IsMouseVerticesMovingEnabled = true;
                    IsEgesAddingEnabled = false;
                    _state = State.Nothing;
                    UserActionsManager.RegisterInfo(Strings.Strings_RU.buttonEdgesOff);
                },
                () => _state == State.Nothing,
                () => true
                )
            {
                Image = new BitmapImage(GetImageUri("Arrow.png")),
                Description = Strings.Strings_RU.buttonEdges
            };
            #endregion

            #region Завершить этап
            var allSubgraphs = new ObservableCollection<IGraph>();

            var phase1Command = new ToolBarInstantCommand(
                () =>
                {
                    var solve = true;
                    CurrentGraph.Vertices.ForEach(v1 =>
                        CurrentGraph.Vertices.ForEach(v2 =>
                            solve = solve && (v1.Equals(v2) || (CurrentGraph[v1, v2] == null ^
                                              GivenGraph[
                                                  GivenGraph.Vertices.Single(v1.Equals),
                                                  GivenGraph.Vertices.Single(v2.Equals)] == null
                            ))
                    ));
                    if (solve)
                    {
                        UserActionsManager.RegisterInfo(Strings.Strings_RU.stage1Done);
                        GivenGraph = CurrentGraph;
                        CurrentGraph = new UndirectedGraph();

                        Phase1ToolBarVisibility = Visibility.Collapsed;
                        Phase2ToolBarVisibility = Visibility.Visible;
                        L1 = Strings.Strings_RU.subgraph;

                        FindAllSubgraphs(GivenGraph, 0, GivenGraph.Vertices.ToList(), allSubgraphs);
                        
                        new HelpDialog(Strings.Strings_RU.stage2Help).Show();
                    }
                    else UserActionsManager.RegisterMistake(Strings.Strings_RU.stage1Mistake1, 10);
                },
                () => _state == State.Nothing
                )
            {
                Image = new BitmapImage(GetImageUri("Check.png")),
                Description = Strings.Strings_RU.stage1DoneButtonDisc
            };
            #endregion

            #region Справка
            var phase1HelpCommand = new ToolBarInstantCommand(
                () =>
                {
                    new HelpDialog(Strings.Strings_RU.stage1Help).Show();
                },
                () => _state == State.Nothing
                )
            {
                Description = Strings.Strings_RU.buttonHelp,
                Image = new BitmapImage(GetImageUri("Info.png"))
            };
            #endregion

            #region Молния
            var thunderbolt1 = new ToolBarInstantCommand(
                () =>
                {
                    CurrentGraph.Vertices.ForEach(v1 =>
                        CurrentGraph.Vertices.ForEach(v2 =>
                        {
                            if (!v1.Equals(v2) && CurrentGraph[v1, v2] == null && GivenGraph[
                                                  GivenGraph.Vertices.Single(v1.Equals),
                                                  GivenGraph.Vertices.Single(v2.Equals)] == null)
                                CurrentGraph.AddEdge(new UndirectedEdge((Vertex)v1, (Vertex)v2));
                            if (!v1.Equals(v2) && CurrentGraph[v1, v2] != null && GivenGraph[
                                                  GivenGraph.Vertices.Single(v1.Equals),
                                                  GivenGraph.Vertices.Single(v2.Equals)] != null)
                                CurrentGraph.RemoveEdge(CurrentGraph[v1, v2]);
                        }
                    ));
                },
                () => _state == State.Nothing
                )
            {
                Description = "Молния",
                Image = new BitmapImage(GetImageUri("thunder.png"))
            };
            #endregion
            #endregion

            #region Второй этап
            #region Добавление вершин
            var vertexDialogCommand = new ToolBarInstantCommand(
                () =>
                {
                    var dialog = new VertexDialog((UndirectedGraph) CurrentGraph, GivenGraph.Vertices);
                    dialog.Show();
                    dialog.Closed += (sender, args) =>
                    {
                        var buf = CurrentGraph;
                        CurrentGraph = null;
                        CurrentGraph = buf;
                    };
                },
                () => _state == State.Nothing)
            {
                Image = new BitmapImage(GetImageUri("Vertexes.png")),
                Description = Strings.Strings_RU.buttonVertexDialog
            };
            #endregion

            #region Добавление рёбер
            var phase2AddEdgeCommand = new ToolBarToggleCommand(
                () =>
                {
                    IsMouseVerticesMovingEnabled = false;
                    IsEgesAddingEnabled = true;
                    _state = State.EdgesAdding;
                    UserActionsManager.RegisterInfo(Strings.Strings_RU.buttonEdgesOn);
                },
                () =>
                {
                    IsMouseVerticesMovingEnabled = true;
                    IsEgesAddingEnabled = false;
                    _state = State.Nothing;
                    UserActionsManager.RegisterInfo(Strings.Strings_RU.buttonEdgesOff);
                },
                () => _state == State.Nothing,
                () => true
                )
            {
                Image = new BitmapImage(GetImageUri("Arrow.png")),
                Description = Strings.Strings_RU.buttonEdges
            };
            #endregion

            #region Добавление подграфов
            var subgraphCommand = new ToolBarInstantCommand(
                () =>
                {
                    var subgraph = true;
                    var unique = Unique((UndirectedGraph)CurrentGraph, GraphLib.Lib);

                    CurrentGraph.Vertices.ForEach(v1 =>
                        CurrentGraph.Vertices.ForEach(v2 =>
                            subgraph &= v1.Equals(v2) || (CurrentGraph[v1, v2] == null ^ GivenGraph[
                                                                        GivenGraph.Vertices.Single(v1.Equals),
                                                                        GivenGraph.Vertices.Single(v2.Equals)] != null)
                    ));
                    if (!subgraph)
                    {
                        UserActionsManager.RegisterMistake(Strings.Strings_RU.stage2Mistake1, 10);
                        return;
                    }

                    if (!unique)
                    {
                        UserActionsManager.RegisterMistake(Strings.Strings_RU.stage2Mistake2, 10);
                        return;
                    }

                    if (CurrentGraph.VerticesCount == 0) return;

                    var newGraph = UndirectedGraph.CreateEmpty(CurrentGraph.VerticesCount);
                    for (var i = 0; i < CurrentGraph.VerticesCount; i++)
                        for (var j = i + 1; j < CurrentGraph.VerticesCount; j++)
                            if (CurrentGraph[CurrentGraph.Vertices[i], CurrentGraph.Vertices[j]] != null)
                                newGraph.AddEdge(new UndirectedEdge(newGraph.Vertices[i], newGraph.Vertices[j]));
                    UserActionsManager.RegisterInfo(Strings.Strings_RU.stage2Subgraph);
                    GraphLib.Lib.Add(newGraph);
                },
                () => _state == State.Nothing
                )
            {
                Image = new BitmapImage(GetImageUri("Subgraph.png")),
                Description = Strings.Strings_RU.buttonCheckSubgraph
            };
            #endregion

            #region Справка
            var phase2HelpCommand = new ToolBarInstantCommand(
                () => new HelpDialog(Strings.Strings_RU.stage2Help).Show(),
                () => _state == State.Nothing
                )
            {
                Description = Strings.Strings_RU.buttonHelp,
                Image = new BitmapImage(GetImageUri("Info.png"))
            };
            #endregion

            #region Молния
            var thunderbolt2 = new ToolBarInstantCommand(
                () =>
                {
                    allSubgraphs.ForEach(s =>
                    {
                        if (Unique(s, GraphLib.Lib))
                            GraphLib.Lib.Add(s);
                    });
                    var g = UndirectedGraph.CreateEmpty(GivenGraph.VerticesCount);
                    for (var i = 0; i < g.VerticesCount; i++)
                        for (var j = i + 1; j < g.VerticesCount; j++)
                            if (GivenGraph[GivenGraph.Vertices[i], GivenGraph.Vertices[j]] != null)
                                g.AddEdge(new UndirectedEdge(g.Vertices[i], g.Vertices[j]));
                    GraphLib.Lib.Add(g);
                },
                () => _state == State.Nothing
                )
            {
                Description = "Молния",
                Image = new BitmapImage(GetImageUri("thunder.png"))
            };
            #endregion

            #region Завершить этап
            var phase2Command = new ToolBarInstantCommand(
                () =>
                {
                    if (GraphLib.Lib.Count > allSubgraphs.Count)
                    {
                        UserActionsManager.RegisterInfo(Strings.Strings_RU.stage2Done);
                        Phase12Visibility = Visibility.Collapsed;
                        Phase3Visibility = Visibility.Visible;
                        Phase2ToolBarVisibility = Visibility.Collapsed;
                        Phase3ToolBarVisibility = Visibility.Visible;
                        BackgroundGraph = new UndirectedGraph(); // Нужно объявить граф, чтобы слой обновился
                        WorkspaceGraph = GivenGraph;
                        BackgroundGraph = GivenGraph;
                        new HelpDialog(Strings.Strings_RU.stage3Help).Show();
                    }
                    else
                    {
                        UserActionsManager.RegisterMistake(Strings.Strings_RU.stage2Mistake3, 10);
                    }
                },
                () => _state == State.Nothing
                )
            {
                Image = new BitmapImage(GetImageUri("Check.png")),
                Description = Strings.Strings_RU.stage2DoneButtonDisc
            };
            #endregion
            #endregion

            #region Третий этап
            #region Справка
            var phase3HelpCommand = new ToolBarInstantCommand(
                () => new HelpDialog(Strings.Strings_RU.stage3Help).Show(),
                () => _state == State.Nothing
                )
            {
                Description = Strings.Strings_RU.buttonHelp,
                Image = new BitmapImage(GetImageUri("Info.png"))
            };
            #endregion

            #region Завершить этап
            var phase3Command = new ToolBarInstantCommand(
                () =>
                {
                    if (WorkspaceGraph.Vertices[0].Name != "True")
                    {
                        UserActionsManager.RegisterMistake(Strings.Strings_RU.stage3Mistake1, 10);
                    }
                    else
                    {
                        UserActionsManager.RegisterInfo(Strings.Strings_RU.stage3Done);
                    }
                },
                () => _state == State.Nothing
                )
            {
                Image = new BitmapImage(GetImageUri("Check.png")),
                Description = Strings.Strings_RU.stage3DoneButtonDisc
            };
            #endregion
            #endregion

            Phase1ToolBarCommands = new ObservableCollection<ToolBarCommandBase>
            {
                phase1AddEdgeCommand,
                phase1Command,
                phase1HelpCommand
                #if DEBUG
                ,
                thunderbolt1
                #endif
            };
            Phase2ToolBarCommands = new ObservableCollection<ToolBarCommandBase>
            {
                vertexDialogCommand,
                phase2AddEdgeCommand,
                subgraphCommand,
                phase2Command,
                phase2HelpCommand
                #if DEBUG
                ,
                thunderbolt2
                #endif
            };
            Phase3ToolBarCommands = new ObservableCollection<ToolBarCommandBase>
            {
                phase3Command,
                phase3HelpCommand
            };
        }
    }
}
