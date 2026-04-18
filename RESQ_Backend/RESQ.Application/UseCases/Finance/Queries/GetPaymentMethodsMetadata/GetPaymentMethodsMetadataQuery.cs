using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetPaymentMethodsMetadata;

/// <summary>
/// Trả về danh sách phương thức thanh toán hiển thị cho FE.
/// Các phương thức bị ẩn (HiddenPaymentMethodAttribute) sẽ không xuất hiện.
/// Kết quả được cache để tránh reflect mỗi request.
/// </summary>
public record GetPaymentMethodsMetadataQuery : IRequest<List<MetadataDto>>;
