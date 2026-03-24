using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Interface;
using TechHub.Service.Service;

namespace TechHub.QuestionBank.Services;

public static class QuestionBankServiceExtensions
{
	/// <summary>
	/// Registers all QuestionBank module services,
	/// repositories and dependencies
	///
	/// DESIGN INTENT:
	/// All repositories explicitly target
	/// DatabaseTarget.QuestionBank
	/// This module never touches the core database
	/// When extracted to microservice:
	/// Only QuestionBankConnection value
	/// in appsettings changes
	/// Zero code changes needed
	/// </summary>
	public static IServiceCollection AddQuestionBankServices(this IServiceCollection services)
	{
		// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		// REPOSITORIES
		// Each explicitly targets QuestionBank database
		// Registered as factory delegates so the
		// connection string key is resolved at
		// registration time not runtime
		// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		// Question repositories
		//services.AddScoped<IQueryRepository<Question>>(provider =>
		//	new QueryRepositoryService<Question>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		//services.AddScoped<ICommandRespository<Question>>(provider =>
		//	new CommandRepositoryService<Question>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		//// QuestionOption repositories
		//services.AddScoped<IQueryRepository<QuestionOption>>(provider =>
		//	new QueryRepositoryService<QuestionOption>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		//services.AddScoped<ICommandRepository<QuestionOption>>(provider =>
		//	new CommandRepositoryService<QuestionOption>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		//// ScanSession repositories
		//services.AddScoped<IQueryRepository<ScanSession>>(provider =>
		//	new QueryRepositoryService<ScanSession>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		//services.AddScoped<ICommandRespository<ScanSession>>(provider =>
		//	new CommandRepositoryService<ScanSession>(
		//		provider.GetRequiredService<IConnectionStringResolver>(),
		//		DatabaseTarget.QuestionBank));

		// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		// SERVICES
		// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		services.AddScoped<IQuestionService, QuestionService>();
		services.AddScoped<IQuestionSyncService, QuestionSyncService>();
		services.AddScoped<IQuestionBoardService, QuestionBoardService>();
		services.AddScoped<IQuestionScanService, QuestionScanService>();


		return services;
	}
}
