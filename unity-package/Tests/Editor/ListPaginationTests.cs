using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ListPaginationTests
    {
        [Test]
        public void PagesFilteredResultsAndReportsBounds()
        {
            var items = CreateItems(120);
            var page = ListPagination.Create(items.Count, 50, 1);

            Assert.AreEqual(50, page.PageSize);
            Assert.AreEqual(1, page.PageIndex);
            Assert.AreEqual(3, page.PageCount);
            Assert.AreEqual(50, page.StartIndex);
            Assert.AreEqual(50, page.CountOnPage);
            Assert.AreEqual(100, page.EndIndexExclusive);
            Assert.IsTrue(page.HasPrevious);
            Assert.IsTrue(page.HasNext);
            Assert.AreEqual(2, page.DisplayPage);

            var slice = page.Slice(items);
            Assert.AreEqual(50, slice.Count);
            Assert.AreEqual("item-050", slice[0]);
            Assert.AreEqual("item-099", slice[49]);
        }

        [Test]
        public void ResetsToFirstPageWhenPageSizeChanges()
        {
            var page = ListPagination.Create(500, 100, 3).WithPageSize(250);
            Assert.AreEqual(250, page.PageSize);
            Assert.AreEqual(0, page.PageIndex);
            Assert.AreEqual(2, page.PageCount);
        }

        [Test]
        public void ClampsOutOfRangePagesAndHandlesEmptyLists()
        {
            var empty = ListPagination.Create(0, 100, 5);
            Assert.AreEqual(0, empty.PageCount);
            Assert.AreEqual(0, empty.PageIndex);
            Assert.AreEqual(0, empty.CountOnPage);
            Assert.AreEqual(0, empty.DisplayPage);
            Assert.IsFalse(empty.HasPrevious);
            Assert.IsFalse(empty.HasNext);
            Assert.AreEqual(0, empty.Slice(Array.Empty<string>()).Count);

            var last = ListPagination.Create(101, 50, 99);
            Assert.AreEqual(2, last.PageIndex);
            Assert.AreEqual(1, last.CountOnPage);
            Assert.IsFalse(last.HasNext);
            Assert.IsTrue(last.HasPrevious);
        }

        [Test]
        public void NormalizesPageSizesToAllowedValues()
        {
            Assert.AreEqual(50, ListPagination.NormalizePageSize(1));
            Assert.AreEqual(100, ListPagination.NormalizePageSize(120));
            Assert.AreEqual(250, ListPagination.NormalizePageSize(200));
            Assert.AreEqual(500, ListPagination.NormalizePageSize(1000));
            Assert.AreEqual(1, ListPagination.IndexOfAllowedPageSize(100));
        }

        [Test]
        public void SliceRejectsMismatchedCounts()
        {
            var page = ListPagination.Create(3, 50, 0);
            Assert.Throws<ArgumentException>(() => page.Slice(new[] { "a", "b" }));
            Assert.Throws<ArgumentNullException>(() => page.Slice<string>(null));
        }

        private static IReadOnlyList<string> CreateItems(int count)
        {
            var items = new string[count];
            for (var i = 0; i < count; i++)
                items[i] = "item-" + i.ToString("D3");
            return items;
        }
    }
}
