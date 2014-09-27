﻿using System;
using System.Collections.Generic;
using System.Data.Entity;

namespace EfEnumToLookup.LookupGenerator
{
    public class EnumToLookup : IEnumToLookup
    {
        public void Apply(DbContext context)
        {
            // recurese through dbsets and references finding anything that uses an enum
            var refs = FindReferences(context.GetType());
            // for the list of enums generate tables
            // t-sql merge values into table
            // add fks from all referencing tables
        }

        internal IList<Reference> FindReferences(Type contextType)
        {
            return new List<Reference>();
        }

        internal class Reference
        {
            public string Source { get; set; }
            public string Destination { get; set; }
        }
    }
}