using System;
using System.Linq.Expressions;

namespace FIBER.Source.Services.Search;

// Построение динамических предикатов для IQueryable (поиск по нескольким словам).
// Реализовано внутри проекта, без внешних зависимостей (System.Linq.Dynamic не используется).
// Все выражения остаются «чистыми» лямбдами — провайдер EF Core SQLite транслирует их в SQL.
public static class PredicateBuilder
{
    // Истинный предикат — нейтральный элемент для накопительного And (true AND P == P).
    public static Expression<Func<T, bool>> Empty<T>() => _ => true;

    // Ложный предикат — нейтральный элемент для накопительного Or.
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    // Логическое И: left AND right (параметры переименовываются к одному).
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = left.Parameters[0];
        var body = Expression.AndAlso(
            new ReplaceParameter(right.Parameters[0], param).Visit(right.Body),
            left.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    // Логическое ИЛИ: left OR right.
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = left.Parameters[0];
        var body = Expression.OrElse(
            new ReplaceParameter(right.Parameters[0], param).Visit(right.Body),
            left.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private sealed class ReplaceParameter : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ReplaceParameter(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
