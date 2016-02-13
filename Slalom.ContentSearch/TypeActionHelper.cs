﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Slalom.ContentSearch
{
    internal static class TypeActionHelper
    {
        public static void Call<T>(Action<T> action, params object[] instances) where T : class
        {
            TypeActionHelper.Call<T>(action, (IEnumerable<object>)instances);
        }

        public static void Call<T>(Action<T> action, IEnumerable<object> instances) where T : class
        {
            foreach (T obj in TypeActionHelper.FilterInstances<T>(instances))
                action(obj);
        }

        private static IEnumerable<T> FilterInstances<T>(IEnumerable<object> instances) where T : class
        {
            foreach (object obj1 in instances)
            {
                if (obj1 != null)
                {
                    T initializable = obj1 as T;
                    if ((object)initializable != null)
                        yield return initializable;
                    else if (!(obj1 is string))
                    {
                        IEnumerable enumerable = obj1 as IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (object obj2 in enumerable)
                            {
                                initializable = obj2 as T;
                                if ((object)initializable != null)
                                    yield return initializable;
                            }
                        }
                    }
                }
            }
        }
    }
}
