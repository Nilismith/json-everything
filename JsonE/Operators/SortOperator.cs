﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.JsonE.Expressions;
using Json.More;

namespace Json.JsonE.Operators;

internal class SortOperator : IOperator
{
	private static readonly Regex _byForm = new(@"^by\(\s*(?<var>[a-zA-Z_][a-zA-Z0-9_]*)\s*\)");

	public const string Name = "$sort";
	
	public void Validate(JsonNode? template)
	{
		var obj = template!.AsObject();

		var parameter = obj[Name];
		if (!parameter.IsTemplateOr<JsonArray>())
			throw new TemplateException(CommonErrors.SortSameType());

		obj.VerifyNoUndefinedProperties(Name, _byForm);

		if (obj.Count > 2)
			throw new TemplateException("Expected no more than two keys");
	}

	public JsonNode? Evaluate(JsonNode? template, EvaluationContext context)
	{
		var obj = template!.AsObject();

		var value = JsonE.Evaluate(obj[Name], context)!.AsArray();
		if (value.Count == 0) return value;

		var accessorEntry = obj.FirstOrDefault(x => x.Key != Name);
		var accessor = ContextAccessor.Root;
		var variableName = "x";

		if (accessorEntry.Key != null)
		{
			int index = 0;
			if (!ContextAccessor.TryParse(accessorEntry.Value!.GetValue<string>().AsSpan(), ref index, out accessor))
				throw new TemplateException("by() requires an accessor");
			variableName = _byForm.Match(accessorEntry.Key).Groups["var"].Value;
		}

		var itemContext = new JsonObject
		{
			[variableName] = value[0].Copy()
		};
		context.Push(itemContext);
		var firstSortValue = context.Find(accessor!);
		var comparer = firstSortValue switch
		{
			JsonValue v when v.TryGetValue<string>(out _) => (IComparer<JsonNode>) JsonNodeStringComparer.Instance,
			JsonValue v when v.GetNumber() != null => JsonNodeNumberComparer.Instance,
			_ => null
		} ?? throw new TemplateException(CommonErrors.SortSameType());


		try
		{
			var sorted = value.OrderBy(x =>
			{
				itemContext[variableName] = x.Copy();
				return context.Find(accessor!);
			}, comparer!);

			return sorted.ToJsonArray();
		}
		catch (InvalidOperationException e)
		{
			// .OrderBy() seems to have its own try/catch which wraps any exceptions.
			// I hate doing this, but I really want the exception thrown by the comparer.
			throw e.InnerException!;
		}
	}
}

internal class JsonNodeStringComparer : IComparer<JsonNode>
{
	public static JsonNodeStringComparer Instance { get; } = new();

	private JsonNodeStringComparer(){}

	public int Compare(JsonNode x, JsonNode y)
	{
		var sX = (x as JsonValue)?.GetValue<string>() ?? throw new TemplateException(CommonErrors.SortSameType());
		var sY = (y as JsonValue)?.GetValue<string>() ?? throw new TemplateException(CommonErrors.SortSameType());

		return string.CompareOrdinal(sX, sY);
	}
}

internal class JsonNodeNumberComparer : IComparer<JsonNode>
{
	public static JsonNodeNumberComparer Instance { get; } = new();

	private JsonNodeNumberComparer(){}

	public int Compare(JsonNode x, JsonNode y)
	{
		var nX = (x as JsonValue)?.GetNumber() ?? throw new TemplateException(CommonErrors.SortSameType());
		var nY = (y as JsonValue)?.GetNumber() ?? throw new TemplateException(CommonErrors.SortSameType());

		return nX <= nY ? -1 : 1;
	}
}