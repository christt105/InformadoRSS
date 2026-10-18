using System.Text.RegularExpressions;
using InformadoRSS.Core;

namespace InformadoRSS.Filters;

/// <summary>
/// A compiled, immutable route filter. Regular expressions are built once per instance, so keep the
/// instance around while the route definition does not change. Safe to use from several threads.
/// </summary>
public sealed class RouteFilter
{
    private readonly Func<FeedItem, bool> _predicate;

    private RouteFilter(Func<FeedItem, bool> predicate) => _predicate = predicate;

    /// <summary>The maximum time a single regular expression evaluation may take.</summary>
    public static TimeSpan RegexTimeout { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>A filter that lets every item through.</summary>
    public static RouteFilter PassAll { get; } = new(static _ => true);

    /// <summary>
    /// Compiles a filter tree. An absent filter, or a root group without children, lets every item through.
    /// </summary>
    /// <exception cref="ArgumentException">The tree has validation errors (see <see cref="FilterValidator"/>).</exception>
    public static RouteFilter Compile(FilterNode? root)
    {
        var errors = FilterValidator.Validate(root);
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid filter: {string.Join("; ", errors)}", nameof(root));
        }

        if (root is null or FilterGroup { Children: null or { Count: 0 } })
        {
            return PassAll;
        }

        return new RouteFilter(Build(root));
    }

    /// <summary>Whether the item passes the filter.</summary>
    /// <exception cref="RegexMatchTimeoutException">A regular expression took longer than <see cref="RegexTimeout"/>.</exception>
    public bool Matches(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _predicate(item);
    }

    internal static Regex CreateRegex(FilterCondition condition)
    {
        var options = RegexOptions.CultureInvariant;
        if (!condition.CaseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(condition.Value, options, RegexTimeout);
    }

    private static Func<FeedItem, bool> Build(FilterNode node)
    {
        var predicate = node switch
        {
            AllGroup group => All(group.Children.Select(Build).ToArray()),
            AnyGroup group => Any(group.Children.Select(Build).ToArray()),
            FilterCondition condition => Condition(condition),
            _ => throw new ArgumentException($"Unsupported filter node {node.GetType().Name}", nameof(node)),
        };

        return node.Negate ? item => !predicate(item) : predicate;
    }

    private static Func<FeedItem, bool> All(Func<FeedItem, bool>[] children) =>
        item => Array.TrueForAll(children, child => child(item));

    private static Func<FeedItem, bool> Any(Func<FeedItem, bool>[] children) =>
        item => Array.Exists(children, child => child(item));

    private static Func<FeedItem, bool> Condition(FilterCondition condition)
    {
        var test = Comparison(condition);
        return condition.Field switch
        {
            FilterField.Title => item => item.Title is { } value && test(value),
            FilterField.Description => item => item.DescriptionText is { } value && test(value),
            FilterField.Author => item => item.Author is { } value && test(value),
            FilterField.Link => item => item.Link is { } value && test(value),
            FilterField.Category when condition.Domain is { } domain => item => item.Categories.Any(category =>
                string.Equals(category.Domain, domain, StringComparison.OrdinalIgnoreCase) && test(category.Value)),
            FilterField.Category => item => item.Categories.Any(category => test(category.Value)),
            _ => throw new ArgumentException($"Unsupported filter field {condition.Field}", nameof(condition)),
        };
    }

    private static Func<string, bool> Comparison(FilterCondition condition)
    {
        var expected = condition.Value;
        var comparison = condition.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        switch (condition.Operator)
        {
            case FilterOperator.Contains:
                return value => value.Contains(expected, comparison);
            case FilterOperator.Equal:
                return value => string.Equals(value, expected, comparison);
            case FilterOperator.StartsWith:
                return value => value.StartsWith(expected, comparison);
            case FilterOperator.Regex:
                var regex = CreateRegex(condition);
                return regex.IsMatch;
            default:
                throw new ArgumentException($"Unsupported filter operator {condition.Operator}", nameof(condition));
        }
    }
}
