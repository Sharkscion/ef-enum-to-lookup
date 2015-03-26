using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace EfEnumToLookup.LookupGenerator
{
	internal class EnumParser
	{
		public EnumParser()
		{
			// default settings
			SplitWords = true;
		}

		/// <summary>
		/// If set to true (default) enum names will have spaces inserted between
		/// PascalCase words, e.g. enum SomeValue is stored as "Some Value".
		/// </summary>
		public bool SplitWords { get; set; }

		public IEnumerable<LookupValue> GetLookupValues(Type lookup)
		{
			if (!lookup.IsEnum)
			{
				throw new ArgumentException("Lookup type must be an enum", "lookup");
			}

			var values = new List<LookupValue>();
			foreach (Enum value in Enum.GetValues(lookup))
			{
				if (IsRuntimeOnly(value))
				{
					continue;
				}

				// avoid cast error for byte enums by converting to int before using a cast
				// https://github.com/timabell/ef-enum-to-lookup/issues/20
				var numericValue = Convert.ChangeType(value, typeof(int));

				values.Add(new LookupValue
				{
					Id = (int)numericValue,
					Name = EnumName(value),
				});
			}
			return values;
		}

		private string EnumName(Enum value)
		{
			var description = EnumDescriptionValue(value);
			if (description != null)
			{
				return description;
			}

			var name = value.ToString();

			if (SplitWords)
			{
				return SplitCamelCase(name);
			}
			return name;
		}

		private static string SplitCamelCase(string name)
		{
			// http://stackoverflow.com/questions/773303/splitting-camelcase/25876326#25876326
			name = Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1", RegexOptions.Compiled);
			return name;
		}

		/// <summary>
		/// Returns the value of the DescriptionAttribute for an enum value,
		/// or null if there isn't one.
		/// </summary>
		private static string EnumDescriptionValue(Enum value)
		{
			var enumType = value.GetType();
			if (!enumType.IsEnum)
			{
				throw new ArgumentException("Lookup type must be an enum", "value");
			}

			// https://stackoverflow.com/questions/1799370/getting-attributes-of-enums-value/1799401#1799401
			var member = enumType.GetMember(value.ToString()).First();
			var description = member.GetCustomAttributes(typeof(DescriptionAttribute)).FirstOrDefault() as DescriptionAttribute;
			return description == null ? null : description.Description;
		}

		private static bool IsRuntimeOnly(Enum value)
		{
			var enumType = value.GetType();

			// https://stackoverflow.com/questions/1799370/getting-attributes-of-enums-value/1799401#1799401
			var member = enumType.GetMember(value.ToString()).First();
			return member.GetCustomAttributes(typeof(RuntimeOnlyAttribute)).Any();
		}
	}
}