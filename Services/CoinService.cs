using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class CoinService : ICoinService
    {
        private readonly ICoinRepository _coinRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public CoinService(ICoinRepository coinRepository, IUserRepository userRepository, IMapper mapper)
        {
            _coinRepository = coinRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<CoinBalanceDto> GetUserCoinBalanceAsync(string userId)
        {
            var balance = await _coinRepository.GetUserCoinBalanceAsync(userId);
            return new CoinBalanceDto { Balance = balance };
        }

        public async Task<IEnumerable<CoinTransactionDto>> GetUserTransactionsAsync(string userId)
        {
            var transactions = await _coinRepository.GetUserTransactionsAsync(userId);
            return _mapper.Map<IEnumerable<CoinTransactionDto>>(transactions);
        }

        public async Task<CoinBalanceDto> PurchaseCoinsAsync(string userId, PurchaseCoinRequest request)
        {
            // In a real application, you would process the payment here
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            user.Coins += request.Amount;
            await _userRepository.UpdateAsync(user);

            var transaction = new CoinTransaction
            {
                UserId = userId,
                Amount = request.Amount,
                Type = "purchased",
                Description = $"Purchased {request.Amount} coins via {request.PaymentMethod}"
            };

            await _coinRepository.CreateTransactionAsync(transaction);

            return new CoinBalanceDto { Balance = user.Coins };
        }

        public async Task<bool> SpendCoinsAsync(string userId, int amount, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Coins < amount)
                return false;

            user.Coins -= amount;
            await _userRepository.UpdateAsync(user);

            var transaction = new CoinTransaction
            {
                UserId = userId,
                Amount = -amount,
                Type = "spent",
                Description = description
            };

            await _coinRepository.CreateTransactionAsync(transaction);
            return true;
        }

        public async Task<bool> EarnCoinsAsync(string userId, int amount, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            user.Coins += amount;
            await _userRepository.UpdateAsync(user);

            var transaction = new CoinTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = "earned",
                Description = description
            };

            await _coinRepository.CreateTransactionAsync(transaction);
            return true;
        }
    }
}
