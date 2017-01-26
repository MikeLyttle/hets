﻿/*
 * REST API Documentation for the MOTI Hired Equipment Tracking System (HETS) Application
 *
 * The Hired Equipment Program is for owners/operators who have a dump truck, bulldozer, backhoe or  other piece of equipment they want to hire out to the transportation ministry for day labour and  emergency projects.  The Hired Equipment Program distributes available work to local equipment owners. The program is  based on seniority and is designed to deliver work to registered users fairly and efficiently  through the development of local area call-out lists. 
 *
 * OpenAPI spec version: 1.0.0
 * 
 * 
 */

using Microsoft.Extensions.DependencyInjection;
using HETSAPI.Services;
using HETSAPI.Services.Impl;

namespace HETSAPI
{
    /// <summary>
    /// Utility extension added to aspnet core to facilitate registration of application-specific services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds application-specific services to the dependency injection container in aspnet core.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
        {
            services.AddTransient<ICityService, CityService>();
            services.AddTransient<IDistrictService, DistrictService>();
            services.AddTransient<IGroupService, GroupService>();            
            services.AddTransient<IPermissionService, PermissionService>();
            services.AddTransient<IRegionService, RegionService>();
            services.AddTransient<IRoleService, RoleService>();            
            services.AddTransient<IServiceAreaService, ServiceAreaService>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<ICurrentUserService, CurrentUserService>();
            return services;
        }
    }
}
