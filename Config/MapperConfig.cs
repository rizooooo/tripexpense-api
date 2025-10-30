using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using TripExpenseApi.Models;
using TripExpenseApi.Models.Dtos;

namespace TripExpenseApi.Config
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            // User mappings
            CreateMap<User, UserDto>();
            CreateMap<UserCreateDto, User>();

            // Trip mappings
            CreateMap<Trip, TripDto>()
                .ForMember(
                    dest => dest.MemberCount,
                    opt => opt.MapFrom(src => src.Members.Count(m => m.IsActive))
                )
                .ForMember(
                    dest => dest.TotalExpenses,
                    opt => opt.MapFrom(src => src.Expenses.Sum(e => e.Amount))
                );

            CreateMap<Trip, TripSummaryDto>()
                .ForMember(
                    dest => dest.MemberCount,
                    opt => opt.MapFrom(src => src.Members.Count(m => m.IsActive))
                )
                .ForMember(
                    dest => dest.TotalExpenses,
                    opt => opt.MapFrom(src => src.Expenses.Sum(e => e.Amount))
                );

            CreateMap<TripCreateDto, Trip>();

            // TripMember mappings
            CreateMap<TripMember, TripMemberDto>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => src.User.Avatar));

            // Expense mappings
            CreateMap<Expense, ExpenseDto>()
                .ForMember(dest => dest.PaidByName, opt => opt.MapFrom(src => src.PaidBy.Name))
                .ForMember(dest => dest.SplitCount, opt => opt.MapFrom(src => src.Splits.Count));

            CreateMap<ExpenseCreateDto, Expense>();

            // ExpenseSplit mappings
            CreateMap<ExpenseSplit, ExpenseSplitDto>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Name));

            CreateMap<ExpenseSplitCreateDto, ExpenseSplit>();

            // Settlement mappings
            CreateMap<Settlement, SettlementDto>()
                .ForMember(dest => dest.FromUserName, opt => opt.MapFrom(src => src.FromUser.Name))
                .ForMember(dest => dest.ToUserName, opt => opt.MapFrom(src => src.ToUser.Name));

            CreateMap<SettlementCreateDto, Settlement>();
        }
    }
}
