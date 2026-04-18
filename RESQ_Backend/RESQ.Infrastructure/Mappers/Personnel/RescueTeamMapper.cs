using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Personnel;
using System.Reflection;

namespace RESQ.Infrastructure.Mappers.Personnel;

public static class RescueTeamMapper
{
    public static RescueTeam ToEntity(RescueTeamModel domain, RescueTeam? existingEntity = null)
    {
        var entity = existingEntity ?? new RescueTeam();
        
        if (domain.Id > 0)
        {
            entity.Id = domain.Id;
        }

        entity.Code = domain.Code;
        entity.Name = domain.Name;
        entity.TeamType = domain.TeamType.ToString();
        entity.Status = domain.Status.ToString();
        entity.AssemblyPointId = domain.AssemblyPointId;
        entity.MaxMembers = domain.MaxMembers;
        entity.ManagedBy = domain.ManagedBy;
        entity.CreatedAt = domain.CreatedAt;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.DisbandAt = domain.DisbandAt;

        return entity;
    }

    public static RescueTeamModel ToDomain(RescueTeam entity, List<RescueTeamMember> members)
    {
        Enum.TryParse<RescueTeamType>(entity.TeamType, ignoreCase: true, out var type);
        Enum.TryParse<RescueTeamStatus>(entity.Status, ignoreCase: true, out var status);

        var domain = (RescueTeamModel)Activator.CreateInstance(typeof(RescueTeamModel), nonPublic: true)!;
        
        SetPrivateProperty(domain, nameof(domain.Id), entity.Id);
        SetPrivateProperty(domain, nameof(domain.Code), entity.Code ?? string.Empty);
        SetPrivateProperty(domain, nameof(domain.Name), entity.Name ?? string.Empty);
        SetPrivateProperty(domain, nameof(domain.TeamType), type);
        SetPrivateProperty(domain, nameof(domain.Status), status);
        SetPrivateProperty(domain, nameof(domain.AssemblyPointId), entity.AssemblyPointId ?? 0);
        
        if (entity.ManagedBy.HasValue)
        {
            SetPrivateProperty(domain, nameof(domain.ManagedBy), entity.ManagedBy.Value);
        }

        if (entity.AssemblyPoint != null && !string.IsNullOrEmpty(entity.AssemblyPoint.Name))
        {
            domain.LoadAssemblyPointName(entity.AssemblyPoint.Name);
        }

        if (entity.AssemblyPoint?.Location != null)
        {
            domain.LoadAssemblyPointLocation(
                new GeoLocation(entity.AssemblyPoint.Location.Y, entity.AssemblyPoint.Location.X));
        }

        SetPrivateProperty(domain, nameof(domain.MaxMembers), entity.MaxMembers ?? 8);
        SetPrivateProperty(domain, nameof(domain.CreatedAt), entity.CreatedAt ?? DateTime.UtcNow);
        SetPrivateProperty(domain, nameof(domain.UpdatedAt), entity.UpdatedAt);
        SetPrivateProperty(domain, nameof(domain.DisbandAt), entity.DisbandAt);

        var memberModels = members.Select(m =>
        {
            Enum.TryParse<TeamMemberStatus>(m.Status, ignoreCase: true, out var memStatus);
            var mem = (RescueTeamMemberModel)Activator.CreateInstance(typeof(RescueTeamMemberModel), nonPublic: true)!;
            SetPrivateProperty(mem, nameof(mem.TeamId), m.TeamId);
            SetPrivateProperty(mem, nameof(mem.UserId), m.UserId);
            SetPrivateProperty(mem, nameof(mem.Status), memStatus);
            SetPrivateProperty(mem, nameof(mem.JoinedAt), m.InvitedAt); // DB column still "invited_at", maps to JoinedAt
            SetPrivateProperty(mem, nameof(mem.IsLeader), m.IsLeader);
            SetPrivateProperty(mem, nameof(mem.RoleInTeam), m.RoleInTeam);

            if (m.User != null)
            {
                var profile = new RescuerProfile(
                    m.User.FirstName,
                    m.User.LastName,
                    m.User.Phone,
                    m.User.Email,
                    m.User.AvatarUrl,
                    m.User.RescuerProfile?.RescuerType
                );
                mem.LoadProfile(profile);
            }

            return mem;
        });

        domain.LoadMembers(memberModels);
        return domain;
    }

    private static void SetPrivateProperty(object instance, string propertyName, object? value)
    {
        var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        prop?.SetValue(instance, value);
    }
}
