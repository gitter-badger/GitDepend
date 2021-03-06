﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using GitDepend.Configuration;

namespace GitDepend.Visitors
{
	/// <summary>
	/// Implementation of <see cref="IVisitor"/> that runs the build for dependencies, and updates the project
	/// to consume the latest dependency artifacts.
	/// </summary>
	public class BuildAndUpdateDependenciesVisitor : IVisitor
	{
		private static readonly Regex Pattern = new Regex(@"^(?<id>.*?)\.(?<version>(?:\d\.){2,3}\d(?:-.*?)?)$", RegexOptions.Compiled);

		private static string CacheDirectory
		{
			get
			{
				var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitDepend");

				var cacheDir = Path.Combine(dir, "cache");
				if (Directory.Exists(cacheDir))
				{
					return cacheDir;
				}

				try
				{
					Directory.CreateDirectory(cacheDir);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine(ex.Message);
					return null;
				}

				return cacheDir;
			}
		}

		#region Implementation of IVisitor

		/// <summary>
		/// The return code
		/// </summary>
		public int ReturnCode { get; set; }

		/// <summary>
		/// Visits a project dependency.
		/// </summary>
		/// <param name="dependency">The <see cref="Dependency"/> to visit.</param>
		/// <returns>The return code.</returns>
		public int VisitDependency(Dependency dependency)
		{
			string dir;
			string error;
			var config = GitDependFile.LoadFromDir(dependency.Directory, out dir, out error);

			if (config == null)
			{
				return ReturnCodes.GitRepositoryNotFound;
			}

			var info = new ProcessStartInfo(Path.Combine(dependency.Directory, config.Build.Script), config.Build.Arguments)
			{
				WorkingDirectory = dependency.Directory,
				UseShellExecute = false
			};
			var proc = Process.Start(info);
			proc?.WaitForExit();

			
			var artifactsDir = dependency.Configuration.Packages.Directory;
			foreach (var file in Directory.GetFiles(artifactsDir, "*.nupkg"))
			{
				var name = Path.GetFileName(file);
				if (!string.IsNullOrEmpty(name))
				{
					File.Copy(file, Path.Combine(CacheDirectory, name), true);
				}
			}

			return proc?.ExitCode ?? ReturnCodes.FailedToRunBuildScript;
		}

		/// <summary>
		/// Visists a project.
		/// </summary>
		/// <param name="directory">The directory of the project.</param>
		/// <param name="config">The <see cref="GitDependFile"/> with project configuration information.</param>
		/// <returns>The return code.</returns>
		public int VisitProject(string directory, GitDependFile config)
		{
			foreach (var dependency in config.Dependencies)
			{
				var dir = dependency.Configuration.Packages.Directory;
				foreach (var file in Directory.GetFiles(dir, "*.nupkg"))
				{
					var name = Path.GetFileNameWithoutExtension(file);


					if (string.IsNullOrEmpty(name))
					{
						continue;
					}

					var match = Pattern.Match(name);

					if (!match.Success)
					{
						continue;
					}

					var id = match.Groups["id"].Value;
					var version = match.Groups["version"].Value;

					var nugetConfig = Path.Combine(directory, "NuGet.config");
					if (!File.Exists(nugetConfig))
					{
						nugetConfig = null;
					}

					var nuget = new Nuget(nugetConfig);

					foreach (var solution in Directory.GetFiles(directory, "*.sln", SearchOption.AllDirectories))
					{
						nuget.Update(solution, id, version, CacheDirectory);
					}
				}
			}

			Console.WriteLine("================================================================================");
			Console.WriteLine($"Making update commit on {directory}");
			var git = new Git(directory);
			git.Add("*.csproj", @"*\packages.config");
			Console.WriteLine("================================================================================");
			git.Status();
			git.Commit("GitDepend: updating dependencies");

			return ReturnCodes.Success;
		}

		#endregion
	}
}
