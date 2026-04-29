using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Maintenance.Commands.SyncSeedData;

public sealed record SyncSeedDataCommand(bool DryRun) : IRequest<SeedDataSyncReport>;
