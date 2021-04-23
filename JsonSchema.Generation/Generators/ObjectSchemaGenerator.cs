﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Json.Schema.Generation.Intents;

namespace Json.Schema.Generation.Generators
{
	internal class ObjectSchemaGenerator : ISchemaGenerator
	{
		public bool Handles(Type type)
		{
			return true;
		}

		public void AddConstraints(SchemaGeneratorContext context)
		{
			context.Intents.Add(new TypeIntent(SchemaValueType.Object));

			var props = new Dictionary<string, SchemaGeneratorContext>();
			var required = new List<string>();
			var propertiesToGenerate = context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead && p.CanWrite);
			var fieldsToGenerate = context.Type.GetFields(BindingFlags.Public | BindingFlags.Instance);
			var hiddenPropertiesToGenerate = context.Type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(p => p.GetCustomAttribute<JsonIncludeAttribute>() != null);
			var hiddenFieldsToGenerate = context.Type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(p => p.GetCustomAttribute<JsonIncludeAttribute>() != null);
			var membersToGenerate = propertiesToGenerate.Cast<MemberInfo>()
				.Concat(fieldsToGenerate)
				.Concat(hiddenPropertiesToGenerate)
				.Concat(hiddenFieldsToGenerate)
				.OrderBy(m => m.Name);

			foreach (var member in membersToGenerate)
			{
				var memberAttributes = member.GetCustomAttributes().ToList();
				var ignoreAttribute = memberAttributes.OfType<JsonIgnoreAttribute>().FirstOrDefault();
				if (ignoreAttribute != null) continue;

				var memberContext = SchemaGenerationContextCache.Get(member.GetMemberType(), memberAttributes, context.Configuration);

				var name = member.Name;
				var nameAttribute = memberAttributes.OfType<JsonPropertyNameAttribute>().FirstOrDefault();
				if (nameAttribute != null)
					name = nameAttribute.Name;

				if (memberAttributes.OfType<ObsoleteAttribute>().Any())
					memberContext.Intents.Add(new DeprecatedIntent(true));

				props.Add(name, memberContext);

				if (memberAttributes.OfType<RequiredAttribute>().Any())
					required.Add(name);
			}

			context.Intents.Add(new PropertiesIntent(props)); 
			if (required.Any())
				context.Intents.Add(new RequiredIntent(required));
		}
	}
}