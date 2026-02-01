using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Application.Behaviours;

public class DomainExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (DomainException ex)
        {
            // Transform Domain Business Rule violations into Bad Requests (HTTP 400)
            throw new BadRequestException(ex.Message);
        }
    }
}