using FluentAssertions;
using System.Diagnostics;
using Xunit.Abstractions;

namespace ECK1.Integration.Cache.ShortTerm.Tests;

public class FieldMaskBenchmark(ITestOutputHelper output)
{
    private const int Iterations = 10000;

    [Fact]
    public void RunEqualityCheck()
    {
        foreach (var (_, mask, count) in GetTestData())
        {
            var src = CreateSource(count);
            var (
                copyParentId,
                copyParentString,
                copyInnerRef,
                copyInner,
                copyInnerSubItemRef,
                copyInnerSubItem,
                copyInnerSubSubItemRef,
                copyInnerSubSubItem,
                copyCollectionRef,
                copyCollection,
                copyCollectionSubItemRef,
                copyCollectionSubItem) = AnalyzeMask(mask);

            var copiedManually = StaticCopy(
                src,
                mask,
                copyParentId,
                copyParentString,
                copyInnerRef,
                copyInner,
                copyInnerSubItemRef,
                copyInnerSubItem,
                copyInnerSubSubItemRef,
                copyInnerSubSubItem,
                copyCollectionRef,
                copyCollection,
                copyCollectionSubItemRef,
                copyCollectionSubItem);

            var (Item, _) = ECK1.Integration.EntityStore.FieldMask.Generated.FieldMaskApplier_ECK1_Integration_Cache_ShortTerm_Tests_Parent.ApplyRoot(src, mask);

            copiedManually.Should().BeEquivalentTo(Item);
        }
    }

#if DEBUG
    [Fact]
#else
    [Fact (Skip = "Benchmark: To be run manually")]
#endif
    public void RunBenchmark()
    {
        output.WriteLine($"Running {Iterations:N0} iterations per test...\n");

        foreach (var (scenario, masks, count) in GetTestData())
        {
            RunTest(scenario, masks, CreateSource(count));
        }
    }

    private static Parent CreateSource(int collectionSize)
    {
        var list = new List<InnerItem>(collectionSize);
        for (int i = 0; i < collectionSize; i++)
        {
            list.Add(new InnerItem
            {
                InnerItemId = $"ID-{i}",
                InnerItemString = $"Str-{i}",
                SubItem = new InnerSubItem
                {
                    InnerItemId = $"IIId-{i}",
                    InnerItemString = $"IIStr-{i}"
                }
            });
        }

        return new Parent
        {
            ParentId = "PID-1",
            ParentString = "Top",
            Inner = new Inner
            {
                InnerId = "IID-1",
                InnerString = "InnerStr",
                SubItem = new InnerItem
                {
                    InnerItemId = "SUB-ID-1",
                    InnerItemString = "SubStr",
                    SubItem = new InnerSubItem
                    {
                        InnerItemId = $"IIId-1",
                        InnerItemString = $"IIStr-1"
                    }
                }
            },
            InnerCollection = list
        };
    }

    private void RunTest(string title, string[] mask, Parent src)
    {
        output.WriteLine(title);
        output.WriteLine($"Mask: {string.Join(", ", mask)}");
        output.WriteLine($"Collection size: {src.InnerCollection.Count}");

        var (
            copyParentId,
            copyParentString,
            copyInnerRef,
            copyInner,
            copyInnerSubItemRef,
            copyInnerSubItem,
            copyInnerSubSubItemRef,
            copyInnerSubSubItem,
            copyCollectionRef,
            copyCollection,
            copyCollectionSubItemRef,
            copyCollectionSubItem) = AnalyzeMask(mask);

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
            _ = StaticCopy(
                src,
                mask,
                copyParentId,
                copyParentString,
                copyInnerRef,
                copyInner,
                copyInnerSubItemRef,
                copyInnerSubItem,
                copyInnerSubSubItemRef,
                copyInnerSubSubItem,
                copyCollectionRef,
                copyCollection,
                copyCollectionSubItemRef,
                copyCollectionSubItem);
            //_ = StaticCopy_NoBoolChecks(src);
        sw2.Stop();

        // warmup
        var fromGenWarmup = ECK1.Integration.EntityStore.FieldMask.Generated.FieldMaskApplier_ECK1_Integration_Cache_ShortTerm_Tests_Parent.ApplyRoot(src, mask);
        
        var sw3 = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            var fromgen = ECK1.Integration.EntityStore.FieldMask.Generated.FieldMaskApplier_ECK1_Integration_Cache_ShortTerm_Tests_Parent.ApplyRoot(
                src,
                null,
                fromGenWarmup.MaskMapCachedHash);
        }
        sw3.Stop();

        output.WriteLine($"Static copy:      {sw2.ElapsedMilliseconds,8} ms");
        output.WriteLine($"FieldMaskGenerator: {sw3.ElapsedMilliseconds,8} ms");
        output.WriteLine($"FieldMaskGenerator Speed ratio:      {(double)sw3.ElapsedMilliseconds / sw2.ElapsedMilliseconds:0.00}x");
    }

    private static Parent StaticCopy(
        Parent src,
        string[] mask,
        bool copyParentId,
        bool copyParentString,
        bool copyInnerRef,
        bool copyInner,
        bool copyInnerSubItemRef,
        bool copyInnerSubItem,
        bool copyInnerSubSubItemRef,
        bool copyInnerSubSubItem,
        bool copyCollectionRef,
        bool copyCollection,
        bool copyCollectionSubItemRef,
        bool copyCollectionSubItem)
    {
        return new Parent
        {
            ParentId = copyParentId ? src.ParentId : default!,
            ParentString = copyParentString ? src.ParentString : default!,
            Inner = copyInnerRef ? 
                src.Inner : copyInner ?
                new Inner
                {
                    InnerId = src.Inner.InnerId,
                    SubItem = copyInnerSubItemRef ?
                        src.Inner.SubItem : copyInnerSubItem ?
                                            new InnerItem 
                                            { 
                                                InnerItemId = src.Inner.SubItem.InnerItemId,
                                                SubItem = copyInnerSubSubItemRef ?
                                                        src.Inner.SubItem.SubItem : copyInnerSubSubItem ?
                                                                                    new InnerSubItem
                                                                                    {
                                                                                        InnerItemId = src.Inner.SubItem.SubItem.InnerItemId,
                                                                                    } :
                                                                                    null
                                            } :
                                            null
                } : 
                null,

            InnerCollection =
                copyCollectionRef
                    ? src.InnerCollection :
                        copyCollection ?
                        [.. src.InnerCollection.Select(i => new InnerItem
                        {
                            InnerItemId = i.InnerItemId,
                            SubItem = copyCollectionSubItemRef ?
                                        i.SubItem : copyCollectionSubItem ?
                                                    new InnerSubItem
                                                    {
                                                        InnerItemId = i.SubItem.InnerItemId,
                                                    } :
                                                    null
                        })] :
                        null
        };
    }

    private Parent StaticCopy_NoBoolChecks(Parent src) => new()
    {
        ParentId = src.ParentId,
        ParentString = src.ParentString,
        Inner = new Inner
        {
            InnerId = src.Inner.InnerId,
            SubItem = new InnerItem
            {
                InnerItemId = src.Inner.SubItem.InnerItemId,
                SubItem = new InnerSubItem
                {
                    InnerItemId = src.Inner.SubItem.InnerItemId,
                }
            }
        },

        InnerCollection =
                    [.. src.InnerCollection.Select(i => new InnerItem
                    {
                        InnerItemId = i.InnerItemId,
                        SubItem =   new InnerSubItem
                                    {
                                        InnerItemId = i.SubItem.InnerItemId,
                                    }
                    })]
    };

    private IEnumerable<(string scenario, string[] mask, int collectionCount)> GetTestData()
    {
        yield return ("1️ -  Only top-level props", ["ParentId", "ParentString"], 10);
        yield return ("2️ - Top-level + nested props", ["ParentId", "Inner.InnerId", "Inner.SubItem.InnerItemId", "InnerCollection"], 10);
        yield return ("3️ - Full mask (nested + collection items, 10 items)", ["ParentId", "ParentString", "Inner.InnerId", "Inner.SubItem.InnerItemId", "InnerCollection.InnerItemId"], 10);
        yield return ("4 - Full mask (nested + collection items, 10,000 items)", 
            [
                "ParentId",
                "ParentString",
                "Inner.InnerId",
                "Inner.SubItem.InnerItemId",
                "Inner.SubItem.SubItem.InnerItemId",
                "InnerCollection.InnerItemId",
                "InnerCollection.SubItem.InnerItemId"
            ], 10_000);
    }

    private static (
        bool copyParentId,
        bool copyParentString,
        bool copyInnerRef,
        bool copyInner,
        bool copyInnerSubItemRef,
        bool copyInnerSubItem,
        bool copyInnerSubSubItemRef,
        bool copyInnerSubSubItem,
        bool copyCollectionRef,
        bool copyCollection,
        bool copyCollectionSubItemRef,
        bool copyCollectionSubItem) AnalyzeMask(string[] mask)
    {
        bool copyParentId = mask.Contains("ParentId");
        bool copyParentString = mask.Contains("ParentString");
        bool copyInnerRef = mask.Contains("Inner");
        bool copyInner = mask.Any(m => m.StartsWith("Inner."));
        bool copyInnerSubItemRef = mask.Contains("Inner.SubItem");
        bool copyInnerSubItem = mask.Any(m => m.StartsWith("Inner.SubItem."));
        bool copyInnerSubSubItemRef = mask.Contains("Inner.SubItem.SubItem");
        bool copyInnerSubSubItem = mask.Any(m => m.StartsWith("Inner.SubItem.SubItem."));
        bool copyCollectionRef = mask.Contains("InnerCollection");
        bool copyCollection = mask.Any(m => m.StartsWith("InnerCollection."));
        bool copyCollectionSubItemRef = mask.Contains("InnerCollection.SubItem");
        bool copyCollectionSubItem = mask.Any(m => m.StartsWith("InnerCollection.SubItem."));

        return (
            copyParentId,
            copyParentString,
            copyInnerRef,
            copyInner,
            copyInnerSubItemRef,
            copyInnerSubItem,
            copyInnerSubSubItemRef,
            copyInnerSubSubItem,
            copyCollectionRef,
            copyCollection,
            copyCollectionSubItemRef,
            copyCollectionSubItem);
    }
}

