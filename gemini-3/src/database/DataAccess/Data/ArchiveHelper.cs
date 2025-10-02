using System;
using System.Text.Json;
using System.Reflection;
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Data
{
    public static class ArchiveHelper
    {
        /// <summary>
        /// EF model configuration for the Archive entity.
        /// </summary>
        public static void Configure(ModelBuilder builder)
        {
            // Register the Archive entity so migrations include it
            builder.Entity<Archive>(entity =>
            {
                entity.HasKey(e => e.ArchiveId);
                // JSON column mappings are applied via attributes
            });
        }

        /// <summary>
        /// Returns the JSON payload for the given property:
        /// if inputJson is non-empty and valid JSON, it is returned;
        /// otherwise the archive's DB value for that property is returned.
        /// Throws if neither is available.
        /// </summary>
        public static string GetMergedJson(Archive archive, string propertyName, string? inputJson)
        {
            // Attempt to use supplied JSON
            if (!string.IsNullOrWhiteSpace(inputJson))
            {
                try
                {
                    JsonDocument.Parse(inputJson);
                    return inputJson!;
                }
                catch (JsonException)
                {
                    // invalid JSON, fallback
                }
            }

            // Fallback to database value via reflection
            var prop = typeof(Archive).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                throw new ArgumentException($"Unknown archive property '{propertyName}'");

            var dbValue = prop.GetValue(archive) as string;
            if (!string.IsNullOrWhiteSpace(dbValue))
                return dbValue;

            throw new InvalidOperationException($"No valid JSON for property '{propertyName}'");
        }
    }
}