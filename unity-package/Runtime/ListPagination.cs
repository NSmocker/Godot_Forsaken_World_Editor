using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Unity-free helper that slices a filtered result list into pages for the Game Data Browser.
    /// </summary>
    public sealed class ListPagination
    {
        public static readonly int[] AllowedPageSizes = { 50, 100, 250, 500 };

        public int TotalCount { get; }
        public int PageSize { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public int StartIndex { get; }
        public int CountOnPage { get; }
        public int EndIndexExclusive => StartIndex + CountOnPage;
        public bool HasPrevious => PageIndex > 0;
        public bool HasNext => PageIndex + 1 < PageCount;
        public int DisplayPage => PageCount == 0 ? 0 : PageIndex + 1;

        private ListPagination(int totalCount, int pageSize, int pageIndex)
        {
            TotalCount = Math.Max(0, totalCount);
            PageSize = NormalizePageSize(pageSize);
            PageCount = TotalCount == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
            PageIndex = PageCount == 0 ? 0 : Clamp(pageIndex, 0, PageCount - 1);
            StartIndex = PageIndex * PageSize;
            CountOnPage = Math.Min(PageSize, Math.Max(0, TotalCount - StartIndex));
        }

        public static ListPagination Create(int totalCount, int pageSize, int pageIndex)
        {
            return new ListPagination(totalCount, pageSize, pageIndex);
        }

        public ListPagination WithPage(int pageIndex)
        {
            return new ListPagination(TotalCount, PageSize, pageIndex);
        }

        public ListPagination WithPageSize(int pageSize)
        {
            // Changing page size always returns to the first page.
            return new ListPagination(TotalCount, pageSize, 0);
        }

        public ListPagination WithTotalCount(int totalCount)
        {
            return new ListPagination(totalCount, PageSize, PageIndex);
        }

        public IReadOnlyList<T> Slice<T>(IReadOnlyList<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (items.Count != TotalCount)
                throw new ArgumentException(
                    "Item count " + items.Count + " does not match pagination total " + TotalCount,
                    nameof(items));
            if (CountOnPage == 0)
                return Array.Empty<T>();

            var page = new T[CountOnPage];
            for (var i = 0; i < CountOnPage; i++)
                page[i] = items[StartIndex + i];
            return page;
        }

        public static int NormalizePageSize(int pageSize)
        {
            var best = AllowedPageSizes[0];
            var bestDistance = Math.Abs(pageSize - best);
            for (var i = 1; i < AllowedPageSizes.Length; i++)
            {
                var distance = Math.Abs(pageSize - AllowedPageSizes[i]);
                if (distance < bestDistance)
                {
                    best = AllowedPageSizes[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static int IndexOfAllowedPageSize(int pageSize)
        {
            var normalized = NormalizePageSize(pageSize);
            for (var i = 0; i < AllowedPageSizes.Length; i++)
            {
                if (AllowedPageSizes[i] == normalized)
                    return i;
            }

            return 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
