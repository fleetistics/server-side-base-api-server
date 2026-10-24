using System.Collections.Concurrent;
using System.Reflection;
using exs.commons.Json;

namespace api_server.core
{
	/// <summary>
	/// Copies every Optional&lt;T&gt; property of a patch DTO that IsSet onto the same-named
	/// property of an entity, so a patch/create service doesn't need an "if (IsSet)" line per field.
	/// Omitted fields leave the entity untouched; an explicit null clears the field only when the
	/// target property is nullable (otherwise throws - validation is expected to catch that earlier).
	/// Patch properties with no same-named, type-compatible writable entity property are ignored
	/// (e.g. media attachables, which need dedicated handling).
	/// </summary>
	public static class PatchExtensions
	{
		private sealed record PropertyMap(PropertyInfo Source, PropertyInfo Target, PropertyInfo IsSet, PropertyInfo Value, bool TargetAllowsNull);

		private static readonly ConcurrentDictionary<(Type Patch, Type Entity), PropertyMap[]> sMaps = new();

		public static TEntity ApplyPatch<TEntity>(this TEntity entity, object patch) where TEntity : class
		{
			foreach (var map in sMaps.GetOrAdd((patch.GetType(), typeof(TEntity)), static key => BuildMaps(key.Patch, key.Entity)))
			{
				var optional = map.Source.GetValue(patch)!;
				if (!(bool)map.IsSet.GetValue(optional)!) continue;

				var value = map.Value.GetValue(optional);
				if (value == null && !map.TargetAllowsNull)
				{
					throw new InvalidOperationException($"{map.Source.Name} cannot be null.");
				}
				map.Target.SetValue(entity, value);
			}
			return entity;
		}

		private static PropertyMap[] BuildMaps(Type patchType, Type entityType)
		{
			var nullability = new NullabilityInfoContext();
			var maps = new List<PropertyMap>();
			foreach (var source in patchType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (!source.PropertyType.IsGenericType || source.PropertyType.GetGenericTypeDefinition() != typeof(Optional<>)) continue;

				var target = entityType.GetProperty(source.Name, BindingFlags.Public | BindingFlags.Instance);
				if (target is not { CanWrite: true }) continue;

				var value = source.PropertyType.GetProperty(nameof(Optional<object>.Value))!;
				if (!target.PropertyType.IsAssignableFrom(value.PropertyType)) continue;

				var allowsNull = target.PropertyType.IsValueType
					? Nullable.GetUnderlyingType(target.PropertyType) != null
					: nullability.Create(target).WriteState != NullabilityState.NotNull;
				maps.Add(new PropertyMap(source, target, source.PropertyType.GetProperty(nameof(Optional<object>.IsSet))!, value, allowsNull));
			}
			return maps.ToArray();
		}
	}
}
