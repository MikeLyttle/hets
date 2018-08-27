﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HetsData.Model
{
    public partial class DbAppContext
    {
        public string SmUserId { get; set; } = null;
        public string DirectoryName { get; set; } = null;
        public string SmUserGuid { get; set; } = null;

        #region Method for Batch / Bulk Saving of Records

        /// <summary>
        /// For importing data only (mass inserts)
        /// </summary>
        /// <returns></returns>
        public int SaveChangesForImport()
        {
            // update the audit fields for this item.
            IEnumerable<EntityEntry> modifiedEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            Debug.WriteLine("Saving Import Data. Total Entries: " + modifiedEntries.Count());

            int result = SaveChanges();

            return result;
        }

        #endregion

        #region Override SaveChanges Method (include audit data)

        private static bool AuditableEntity(object objectToCheck)
        {
            Type type = objectToCheck.GetType();
            return type.GetProperty("AppCreateUserDirectory") != null;
        }

        private static object GetAuditProperty(object obj, string property)
        {
            PropertyInfo prop = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);

            if (prop != null && prop.CanRead)
            {
                return prop.GetValue(obj);
            }

            return null;
        }

        private static void SetAuditProperty(object obj, string property, object value)
        {
            PropertyInfo prop = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);

            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value, null);
            }
        }

        /// <summary>
        /// Override for Save Changes to implement the audit log
        /// </summary>
        /// <returns></returns>
        public override int SaveChanges()
        {
            // get all of the modified records
            IEnumerable<EntityEntry> modifiedEntries = ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added ||
                                e.State == EntityState.Modified);

            // manage the audit columns and the concurrency column
            DateTime currentTime = DateTime.UtcNow;

            List<HetSeniorityAudit> seniorityAudits = new List<HetSeniorityAudit>();

            foreach (EntityEntry entry in modifiedEntries)
            {
                if (AuditableEntity(entry.Entity))
                {
                    SetAuditProperty(entry.Entity, "AppLastUpdateUserid", SmUserId);
                    SetAuditProperty(entry.Entity, "AppLastUpdateUserDirectory", DirectoryName);
                    SetAuditProperty(entry.Entity, "AppLastUpdateUserGuid", SmUserGuid);
                    SetAuditProperty(entry.Entity, "AppLastUpdateTimestamp", currentTime);
                    
                    if (entry.State == EntityState.Added)
                    {
                        SetAuditProperty(entry.Entity, "AppCreateUserid", SmUserId);
                        SetAuditProperty(entry.Entity, "AppCreateUpdateUserid", SmUserId);
                        SetAuditProperty(entry.Entity, "AppCreateUpdateUserDirectory", DirectoryName);
                        SetAuditProperty(entry.Entity, "AppCreateUpdateUserGuid", SmUserGuid);
                        SetAuditProperty(entry.Entity, "AppCreateTimestamp", currentTime);
                        SetAuditProperty(entry.Entity, "ConcurrencyControlNumber", 1);                        
                    }
                    else
                    {
                        int controlNumber = (int)GetAuditProperty(entry.Entity, "ConcurrencyControlNumber");
                        controlNumber = controlNumber + 1;
                        SetAuditProperty(entry.Entity, "ConcurrencyControlNumber", controlNumber);
                    }
                }

                if (entry.Entity is HetEquipment)
                {
                    DoEquipmentAudit(seniorityAudits, entry, SmUserId);
                }
            }

            // *************************************************
            // attempt to save updates
            // *************************************************
            int result;

            try
            {
                result = base.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                // e.InnerException.Message	"20180: Concurrency Failure 5"	string
                if (e.InnerException != null &&
                    e.InnerException.Message.StartsWith("20180"))
                {
                    // concurrency error
                    throw new HetsDbConcurrencyException("This record has been updated by another user.");
                }

                throw;
            }

            // *************************************************
            // manage seniority audit records
            // *************************************************
            if (seniorityAudits.Count > 0)
            {
                foreach (HetSeniorityAudit seniorityAudit in seniorityAudits)
                {
                    HetSeniorityAudit.Add(seniorityAudit);
                }
            }

            base.SaveChanges();

            return result;
        }

        #endregion

        #region Equipment Audit

        private void DoEquipmentAudit(List<HetSeniorityAudit> audits, EntityEntry entry, string smUserId)
        {
            HetEquipment changed = (HetEquipment)entry.Entity;

            int tempChangedId = changed.EquipmentId;

            // if this is an "empty" record - exit
            if (tempChangedId <= 0)
            {
                return;
            }

            HetEquipment original = HetEquipment.AsNoTracking()
                .Include(x => x.LocalArea)
                .Include(x => x.Owner)
                .First(a => a.EquipmentId == tempChangedId);

            // compare the old and new
            if (changed.IsSeniorityAuditRequired(original))
            {
                DateTime currentTime = DateTime.UtcNow;

                // create the audit entry.
                HetSeniorityAudit seniorityAudit = new HetSeniorityAudit
                {
                    BlockNumber = original.BlockNumber,
                    EndDate = currentTime
                };

                int tempLocalAreaId = original.LocalArea.LocalAreaId;
                int tempOwnerId = original.Owner.OwnerId;

                changed.SeniorityEffectiveDate = currentTime;
                seniorityAudit.AppCreateTimestamp = currentTime;
                seniorityAudit.AppLastUpdateTimestamp = currentTime;
                seniorityAudit.AppCreateUserid = smUserId;
                seniorityAudit.AppLastUpdateUserid = smUserId;

                seniorityAudit.EquipmentId = tempChangedId;
                seniorityAudit.LocalAreaId = tempLocalAreaId;
                seniorityAudit.OwnerId = tempOwnerId;

                if (seniorityAudit.Owner != null)
                {
                    seniorityAudit.OwnerOrganizationName = seniorityAudit.Owner.OrganizationName;
                }

                if (original.SeniorityEffectiveDate != null)
                {
                    seniorityAudit.StartDate = (DateTime)original.SeniorityEffectiveDate;
                }

                seniorityAudit.Seniority = original.Seniority;
                seniorityAudit.ServiceHoursLastYear = original.ServiceHoursLastYear;
                seniorityAudit.ServiceHoursTwoYearsAgo = original.ServiceHoursTwoYearsAgo;
                seniorityAudit.ServiceHoursThreeYearsAgo = original.ServiceHoursThreeYearsAgo;

                audits.Add(seniorityAudit);
            }
        }

        #endregion

        #region User Extensions

        /// <summary>
        /// Load User from HETS database using their userId and guid
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="guid"></param>
        /// <returns></returns>
        public HetUser LoadUser(string userId, string guid = null)
        {
            HetUser user = null;

            if (!string.IsNullOrEmpty(guid))
                user = GetUserByGuid(guid);

            if (user == null)
                user = GetUserBySmUserId(userId);

            if (user == null)
                return null;

            if (guid == null)
                return user;

            if (!string.IsNullOrEmpty(user.Guid))
            {
                // self register (write the users Guid to thd db)
                user.Guid = guid;
                SaveChanges();
            }
            else if (!user.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase))
            {
                // invalid account - guid doesn't match user credential
                return null;
            }

            return user;
        }

        /// <summary>
        /// Returns a user based on the guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public HetUser GetUserByGuid(string guid)
        {
            HetUser user = HetUser
                .Where(x => x.Guid != null &&
                            x.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                .Include(u => u.HetUserRole)
                    .ThenInclude(r => r.Role)
                        .ThenInclude(rp => rp.HetRolePermission)
                            .ThenInclude(p => p.Permission)
                .FirstOrDefault();

            return user;
        }

        /// <summary>
        /// Returns a user based on the account name
        /// </summary>
        /// <param name="smUserId"></param>
        /// <returns></returns>
        public HetUser GetUserBySmUserId(string smUserId)
        {
            HetUser user = HetUser
                .Where(x => x.SmUserId != null && 
                            x.SmUserId.Equals(smUserId, StringComparison.OrdinalIgnoreCase))
                .Include(u => u.HetUserRole)
                    .ThenInclude(r => r.Role)
                        .ThenInclude(rp => rp.HetRolePermission)
                            .ThenInclude(p => p.Permission)
                .FirstOrDefault();

            return user;
        }

        #endregion
    }
}