using System.Collections.Generic;
using NUnit.Framework;
using Rhino.Mocks;
using SolrNet.Attributes;
using SolrNet.Commands.Parameters;
using SolrNet.Utils;

namespace SolrNet.Tests {
    [TestFixture]
    public class SolrQueryExecuterTests {
        public class TestDocument : ISolrDocument {
            [SolrUniqueKey]
            public int Id { get; set; }
        }

        [Test]
        public void Execute() {
            const string queryString = "id:123456";
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.CreateMock<ISolrQueryResultParser<TestDocument>>();
            var mockR = mocks.DynamicMock<ISolrQueryResults<TestDocument>>();
            With.Mocks(mocks).Expecting(delegate {
                var q = new Dictionary<string, string>();
                q["q"] = queryString;
                Expect.Call(conn.Get("/select", q)).Repeat.Once().Return("");
                Expect.Call(parser.Parse(null)).IgnoreArguments().Repeat.Once().Return(mockR);
            }).Verify(delegate {
                var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {ResultParser = parser};
                var r = queryExecuter.Execute(new SolrQuery(queryString), null);
            });
        }

        [Test]
        public void Sort() {
            const string queryString = "id:123456";
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.CreateMock<ISolrQueryResultParser<TestDocument>>();
            var mockR = mocks.DynamicMock<ISolrQueryResults<TestDocument>>();
            var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {
                ResultParser = parser,
            };
            With.Mocks(mocks).Expecting(delegate {
                var q = new Dictionary<string, string>();
                q["q"] = queryString;
                q["rows"] = queryExecuter.DefaultRows.ToString();
                q["sort"] = "id asc";
                Expect.Call(conn.Get("/select", q)).Repeat.Once().Return("");
                Expect.Call(parser.Parse(null)).IgnoreArguments().Repeat.Once().Return(mockR);
            }).Verify(delegate { var r = queryExecuter.Execute(new SolrQuery(queryString), new QueryOptions {OrderBy = new[] {new SortOrder("id")}}); });
        }

        [Test]
        public void SortMultipleWithOrders() {
            const string queryString = "id:123456";
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.CreateMock<ISolrQueryResultParser<TestDocument>>();
            var mockR = mocks.DynamicMock<ISolrQueryResults<TestDocument>>();
            var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {
                ResultParser = parser,
            };
            With.Mocks(mocks).Expecting(delegate {
                var q = new Dictionary<string, string>();
                q["q"] = queryString;
                q["rows"] = queryExecuter.DefaultRows.ToString();
                q["sort"] = "id asc,name desc";
                Expect.Call(conn.Get("/select", q)).Repeat.Once().Return("");
                Expect.Call(parser.Parse(null)).IgnoreArguments().Repeat.Once().Return(mockR);
            }).Verify(delegate {
                var r = queryExecuter.Execute(new SolrQuery(queryString), new QueryOptions {
                    OrderBy = new[] {
                        new SortOrder("id", Order.ASC),
                        new SortOrder("name", Order.DESC)
                    }
                });
            });
        }

        [Test]
        public void RandomSort() {
            const string queryString = "id:123456";
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.CreateMock<ISolrQueryResultParser<TestDocument>>();
            var random = mocks.CreateMock<IListRandomizer>();
            With.Mocks(mocks).Expecting(() => {
                var q = new Dictionary<string, string>();
                q["q"] = queryString;
                q["rows"] = int.MaxValue.ToString();
                q["fl"] = "id";
                Expect.Call(conn.Get("/select", q)).IgnoreArguments().Return("");
                var doc123 = new TestDocument {Id = 123};
                var doc456 = new TestDocument {Id = 456};
                var doc567 = new TestDocument {Id = 567};
                Expect.Call(parser.Parse(null)).IgnoreArguments().Return(new SolrQueryResults<TestDocument> {
                    doc123,
                    doc456,
                    doc567,
                });
                Expect.Call(() => random.Randomize(new List<TestDocument>())).IgnoreArguments();
                var nq = new Dictionary<string, string>();
                nq["q"] = "(id:123 OR id:456 OR id:567)";
                Expect.Call(conn.Get("/select", nq)).IgnoreArguments().Return("");
                Expect.Call(parser.Parse(null)).IgnoreArguments().Return(new SolrQueryResults<TestDocument> {
                    doc123,
                    doc456,
                    doc567,
                });
            }).Verify(() => {
                var e = new SolrQueryExecuter<TestDocument>(conn) {
                    ResultParser = parser,
                    ListRandomizer = random,
                };
                var r = e.Execute(new SolrQuery(queryString), new QueryOptions {
                    OrderBy = SortOrder.Random,
                    Rows = 2,
                });
            });
        }

        [Test]
        public void ResultFields() {
            const string queryString = "id:123456";
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.CreateMock<ISolrQueryResultParser<TestDocument>>();
            var mockR = mocks.DynamicMock<ISolrQueryResults<TestDocument>>();
            var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {
                ResultParser = parser,
            };
            With.Mocks(mocks).Expecting(delegate {
                var q = new Dictionary<string, string>();
                q["q"] = queryString;
                q["rows"] = queryExecuter.DefaultRows.ToString();
                q["fl"] = "id,name";
                Expect.Call(conn.Get("/select", q)).Repeat.Once().Return("");
                Expect.Call(parser.Parse(null)).IgnoreArguments().Repeat.Once().Return(mockR);
            }).Verify(() => {
                var r = queryExecuter.Execute(new SolrQuery(queryString), new QueryOptions {
                    Fields = new[] {"id", "name"},
                });
            });
        }

        [Test]
        public void Facets() {
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.DynamicMock<ISolrQueryResultParser<TestDocument>>();
            var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {
                ResultParser = parser,
            };
            With.Mocks(mocks).Expecting(() => {
                var q = new Dictionary<string, string>();
                q["q"] = "";
                q["rows"] = queryExecuter.DefaultRows.ToString();
                q["facet"] = "true";
                q["facet.field"] = "Id";
                q["facet.query"] = "id:[1 TO 5]";
                Expect.Call(conn.Get("/select", q))
                    .Repeat.Once().Return("");
            }).Verify(() => {
                queryExecuter.Execute(new SolrQuery(""), new QueryOptions {
                    FacetQueries = new ISolrFacetQuery[] {
                        new SolrFacetFieldQuery("Id"),
                        new SolrFacetQuery(new SolrQuery("id:[1 TO 5]")),
                    },
                });
            });
        }

        [Test]
        public void Highlighting() {
            var mocks = new MockRepository();
            var conn = mocks.CreateMock<ISolrConnection>();
            var parser = mocks.DynamicMock<ISolrQueryResultParser<TestDocument>>();
            const string highlightedField = "field1";
            const string afterTerm = "after";
            const string beforeTerm = "before";
            var queryExecuter = new SolrQueryExecuter<TestDocument>(conn) {
                ResultParser = parser,
            };
            With.Mocks(mocks).Expecting(() => {
                var q = new Dictionary<string, string>();
                q["q"] = "";
                q["rows"] = queryExecuter.DefaultRows.ToString();
                q["hl"] = "true";
                q["hl.fl"] = highlightedField;
                q["hl.simple.pre"] = beforeTerm;
                q["hl.simple.post"] = afterTerm;
                Expect.Call(conn.Get("/select", q))
                    .Repeat.Once().Return("");
            }).Verify(() => queryExecuter.Execute(new SolrQuery(""), new QueryOptions {
                Highlight = new HighlightingParameters {
                    Fields = new[] {highlightedField},
                    AfterTerm = afterTerm,
                    BeforeTerm = beforeTerm,
                }
            }));
        }
    }
}