﻿// -----------------------------------------------------------------------
//  <copyright file="OSharpPackManager.cs" company="OSharp开源团队">
//      Copyright (c) 2014-2018 OSharp. All rights reserved.
//  </copyright>
//  <site>http://www.osharp.org</site>
//  <last-editor>郭明锋</last-editor>
//  <last-date>2018-06-23 15:18</last-date>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OSharp.Core.Builders;
using OSharp.Reflection;


namespace OSharp.Core.Packs
{
    /// <summary>
    /// OSharp模块管理器
    /// </summary>
    public class OSharpPackManager
    {
        private readonly IOSharpBuilder _builder;
        private readonly List<OsharpPack> _sourcePacks;
        private readonly OSharpPackTypeFinder _typeFinder;

        /// <summary>
        /// 初始化一个<see cref="OSharpPackManager"/>类型的新实例
        /// </summary>
        public OSharpPackManager(IOSharpBuilder builder, IAllAssemblyFinder allAssemblyFinder)
        {
            _builder = builder;
            _typeFinder = new OSharpPackTypeFinder(allAssemblyFinder);
            _sourcePacks = new List<OsharpPack>();
            LoadedPacks = new List<OsharpPack>();
        }

        /// <summary>
        /// 获取 自动检索到的所有模块信息
        /// </summary>
        public IEnumerable<OsharpPack> SourcePacks => _sourcePacks;

        /// <summary>
        /// 获取 最终加载的模块信息集合
        /// </summary>
        public IEnumerable<OsharpPack> LoadedPacks { get; private set; }

        /// <summary>
        /// 加载模块服务
        /// </summary>
        /// <param name="services">服务容器</param>
        /// <returns></returns>
        public IServiceCollection LoadPacks(IServiceCollection services)
        {
            Type[] packTypes = _typeFinder.FindAll();
            _sourcePacks.Clear();
            _sourcePacks.AddRange(packTypes.Select(m => (OsharpPack)Activator.CreateInstance(m)));
            List<OsharpPack> packs;
            if (_builder.AddPacks.Any())
            {
                packs = _sourcePacks.Where(m => m.Level == PackLevel.Core)
                    .Union(_sourcePacks.Where(m => _builder.AddPacks.Contains(m.GetType()))).Distinct().ToList();
                IEnumerable<Type> dependModuleTypes = packs.SelectMany(m => m.GetDependModuleTypes());
                packs = packs.Union(_sourcePacks.Where(m => dependModuleTypes.Contains(m.GetType()))).Distinct().ToList();
            }
            else
            {
                packs = _sourcePacks.ToList();
                packs.RemoveAll(m => _builder.ExceptPacks.Contains(m.GetType()));
            }
            packs = packs.OrderBy(m => m.Level).ThenBy(m => m.Order).ToList();
            LoadedPacks = packs;

            foreach (OsharpPack pack in LoadedPacks)
            {
                services = pack.AddServices(services);
            }

            return services;
        }

        /// <summary>
        /// 启用模块
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public void UsePacks(IApplicationBuilder app)
        {
            ILogger<OSharpPackManager> logger = app.ApplicationServices.GetService<ILogger<OSharpPackManager>>();
            logger.LogInformation("OSharp框架初始化开始");

            foreach (OsharpPack pack in LoadedPacks)
            {
                pack.UsePack(app);
                logger.LogInformation($"模块{pack.GetType()}加载成功");
            }

            logger.LogInformation("OSharp框架初始化完成");
        }
    }
}