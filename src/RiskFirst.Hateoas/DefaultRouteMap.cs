﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace RiskFirst.Hateoas
{
    public class DefaultRouteMap : IRouteMap
    {
        private readonly IActionContextAccessor contextAccessor;
        private readonly ILogger<DefaultRouteMap> logger;
        private IDictionary<string, RouteInfo> RouteMap { get; } = new Dictionary<string, RouteInfo>();
        public DefaultRouteMap(IActionContextAccessor contextAccessor, ILogger<DefaultRouteMap> logger)
        {
            this.contextAccessor = contextAccessor;
            this.logger = logger;

            var thisAssembly = typeof(DefaultRouteMap).GetTypeInfo().Assembly.GetName().Name;
            var libraries =
                DependencyContext.Default
                                 .CompileLibraries
                                 .Where(l => l.Dependencies.Any(d => d.Name.Equals(thisAssembly)));

            var names = libraries.Select(l => l.Name).Distinct();
            var assemblies = names.Select(a => Assembly.Load(new AssemblyName(a)));

            foreach (var asm in assemblies)
            {
                var controllers = asm.GetTypes()
                    .Where(type => typeof(Controller).IsAssignableFrom(type));

                var controllerMethods = controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                                     .Where(m => m.IsDefined(typeof(HttpMethodAttribute)))
                                                                     .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(), (m, attr) => new
                                                                     {
                                                                         Controller = c,
                                                                         Method = m,
                                                                         HttpAttribute = attr
                                                                     }));

                foreach (var attr in controllerMethods.Where(a => !String.IsNullOrWhiteSpace(a.HttpAttribute.Name)))
                {
                    var method = ParseMethod(attr.HttpAttribute.HttpMethods);
                    RouteMap[attr.HttpAttribute.Name] = new RouteInfo(attr.HttpAttribute.Name, method, new ReflectionControllerMethodInfo(attr.Method));
                }
            }
        }
                
        public RouteInfo GetRoute(string name)
        {
            if (!RouteMap.ContainsKey(name))
            {
                return null;
            }
            return RouteMap[name];
        }

        public RouteInfo GetCurrentRoute()
        {
            var action = this.contextAccessor?.ActionContext?.ActionDescriptor as ControllerActionDescriptor;
            if (action == null)
                throw new InvalidOperationException($"Invalid action descriptor in route map");
            var attr = action.MethodInfo.GetCustomAttribute<HttpMethodAttribute>();
            var method = ParseMethod(attr.HttpMethods);
            return new RouteInfo(attr.Name, method, new ReflectionControllerMethodInfo(action.MethodInfo));
        }

        private HttpMethod ParseMethod(IEnumerable<string> methods)
        {
            HttpMethod method = HttpMethod.Get;
            if (methods != null && methods.Any())
            {
                method = new HttpMethod(methods.First());
            }
            return method;
        }

    }
}
