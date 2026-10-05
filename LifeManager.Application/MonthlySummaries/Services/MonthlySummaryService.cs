using LifeManager.Application.MonthlySummaries.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.MonthlySummaries.Services
{
    public class MonthlySummaryService(IMonthlySummaryRepository monthlySummaryRepository, ITransactionRepository transactionRepository)
    {
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly ITransactionRepository _transactionRepository = transactionRepository;

        public async Task<Result<MonthlySummaryResponseDto>> CreateAsync(MonthlySummaryDto monthlySummaryDto, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummaryResult = MonthlySummary.Create(userId.Value, monthlySummaryDto.Month, DateTimeOffset.UtcNow.Year);
            if (!monthlySummaryResult.IsSuccess)
                return monthlySummaryResult.Error;

            var monthlySummary = monthlySummaryResult.Value;

            if (await _monthlySummaryRepository.ExistsAsync(userId, monthlySummary.Month, monthlySummary.Year, cancellationToken))
                return MonthlySummaryErrors.AlreadyExists;

            await _monthlySummaryRepository.AddAsync(monthlySummary, cancellationToken);

            return ToResponseDto(monthlySummary);
        }

        public async Task<Result<MonthlySummaryDetailsResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(id), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            var counts = await _transactionRepository.CountByTypeAsync(monthlySummary.Id!, cancellationToken);
            var neighbors = await _monthlySummaryRepository.GetNeighborsAsync(userId, monthlySummary.Year, monthlySummary.Month, cancellationToken);

            return new MonthlySummaryDetailsResponseDto(
                monthlySummary.Id!.Value,
                monthlySummary.Month.Value,
                monthlySummary.Year.Value,
                monthlySummary.TotalIncome.Value,
                monthlySummary.TotalExpense.Value,
                monthlySummary.TotalInvestment.Value,
                monthlySummary.Balance.Value,
                counts.IncomeCount,
                counts.ExpenseCount,
                counts.InvestmentCount,
                neighbors.PreviousId,
                neighbors.NextId);
        }

        public async Task<Result<PagedResponseDto<MonthlySummaryResponseDto>>> GetPagedAsync(MonthlySummaryListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var monthlySummaries = await _monthlySummaryRepository.GetPagedByUserIdAsync(
                userId, pageRequestResult.Value, query.Year, query.Balance, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<MonthlySummaryResponseDto>.From(monthlySummaries, ToResponseDto);
        }

        public async Task<Result<int[]>> GetYearsAsync(UserId userId, CancellationToken cancellationToken)
        {
            var years = await _monthlySummaryRepository.GetYearsByUserIdAsync(userId, cancellationToken);

            return years.ToArray();
        }

        private static MonthlySummaryResponseDto ToResponseDto(MonthlySummary monthlySummary)
            => new(
                monthlySummary.Id!.Value,
                monthlySummary.Month.Value,
                monthlySummary.Year.Value,
                monthlySummary.TotalIncome.Value,
                monthlySummary.TotalExpense.Value,
                monthlySummary.TotalInvestment.Value,
                monthlySummary.Balance.Value);
    }
}
