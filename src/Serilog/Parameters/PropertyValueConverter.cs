﻿// Copyright 2013 Nicholas Blumhardt
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Policies;

namespace Serilog.Parameters
{
    class PropertyValueConverter : ILogEventPropertyFactory, ILogEventPropertyValueFactory
    {
        static readonly HashSet<Type> BuiltInScalarTypes = new HashSet<Type>
        {
            typeof(bool),
            typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
            typeof(string),
            typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
            typeof(Guid), typeof(Uri),
            typeof(byte[])
        };

        static readonly List<IDestructuringPolicy> BuiltInDestructuringPolicies = new List<IDestructuringPolicy>
        {
            new NullableDestructuringPolicy()
        }; 

        readonly HashSet<Type> _scalarTypes;
        readonly List<IDestructuringPolicy> _destructuringPolicies; 

        public PropertyValueConverter(IEnumerable<Type> additionalScalarTypes, IEnumerable<IDestructuringPolicy> additionalDestructuringPolicies)
        {
            _scalarTypes = new HashSet<Type>(additionalScalarTypes);
            _scalarTypes.UnionWith(BuiltInScalarTypes);

            _destructuringPolicies = new List<IDestructuringPolicy>(BuiltInDestructuringPolicies.Concat(additionalDestructuringPolicies));
        }

        public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, CreatePropertyValue(value, destructureObjects));
        }

        public LogEventPropertyValue CreatePropertyValue(object value, bool destructureObjects = false)
        {
            return CreatePropertyValue(value, destructureObjects, 1);
        }

        public LogEventPropertyValue CreatePropertyValue(object value, bool destructureObjects, int depth)
        {
            return CreatePropertyValue(
                value,
                destructureObjects ?
                    Destructuring.Destructure :
                    Destructuring.Default,
                depth);
        }

        public LogEventPropertyValue CreatePropertyValue(object value, Destructuring destructuring)
        {
            return CreatePropertyValue(value, destructuring, 1);
        }

        LogEventPropertyValue CreatePropertyValue(object value, Destructuring destructuring, int depth)
        {
            if (value == null)
                return new ScalarValue(null);

            if (destructuring == Destructuring.Stringify)
                return new ScalarValue(value.ToString());

            // Known literals
            var valueType = value.GetType();
            if (_scalarTypes.Contains(valueType) || valueType.GetTypeInfo().IsEnum)
                return new ScalarValue(value);

            // Dictionaries should be treated here, probably as
            // structures...

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                return new SequenceValue(
                    enumerable.Cast<object>().Select(o => CreatePropertyValue(o, destructuring)));
            }

            // Unknown types

            if (destructuring == Destructuring.Destructure)
            {
                var limiter = new DepthLimiter(depth, this);

                foreach (var destructuringPolicy in _destructuringPolicies)
                {
                    LogEventPropertyValue result;
                    if (destructuringPolicy.TryDestructure(value, limiter, out result))
                        return result;
                }

                var typeTag = value.GetType().Name;
                if (typeTag.Length <= 0 || !char.IsLetter(typeTag[0]))
                    typeTag = null;

                return new StructureValue(GetProperties(value, limiter), typeTag);
            }

            return new ScalarValue(value);
        }

        static IEnumerable<LogEventProperty> GetProperties(object value, ILogEventPropertyValueFactory recursive)
        {
            var valueType = value.GetType().GetTypeInfo();
            while (valueType.AsType() != typeof(object))
            {
                var props = valueType.DeclaredProperties.Where(p => p.CanRead &&
                                                                    p.GetMethod.IsPublic &&
                                                                    !p.GetMethod.IsStatic);

                foreach (var prop in props)
                {
                    yield return new LogEventProperty(prop.Name, recursive.CreatePropertyValue(prop.GetValue(value), true));
                }
                
                valueType = valueType.BaseType.GetTypeInfo();
            }
        }
    }
}
