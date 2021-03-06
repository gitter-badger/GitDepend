﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GitDepend.Configuration
{
	/// <summary>
	/// Represents GitDepend.json in memory.
	/// </summary>
	public class GitDependFile
	{
		private List<Dependency> _dependencies;
		private Build _build;
		private Packages _packages;

		/// <summary>
		/// The build section.
		/// </summary>
		[JsonProperty("build")]
		public Build Build => _build ?? (_build = new Build());

		/// <summary>
		/// The packages section
		/// </summary>
		[JsonProperty("packages")]
		public Packages Packages => _packages ?? (_packages = new Packages());

		/// <summary>
		/// The dependencies section
		/// </summary>
		[JsonProperty("dependencies")]
		public List<Dependency> Dependencies => _dependencies ?? (_dependencies = new List<Dependency>());

		#region Overrides of Object

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		#endregion

		/// <summary>
		/// Finds a GitDepend.json file in the given directory and loads it into memory.
		/// </summary>
		/// <param name="directory">The directory to start in.</param>
		/// <param name="dir">The directory where GitDepend.json was found.</param>
		/// <param name="error">An error string indicating what went wrong in the case that the file could not be loaded.</param>
		/// <returns></returns>
		public static GitDependFile LoadFromDir(string directory, out string dir, out string error)
		{
			if (!Directory.Exists(directory))
			{
				dir = null;
				error = $"{directory} does not exist";
				return null;
			}

			dir = directory;
			var current = directory;
			bool isGitRoot;
			do
			{
				isGitRoot = Directory.GetDirectories(current, ".git").Any();

				if (!isGitRoot)
				{
					current = Directory.GetParent(current)?.FullName;
				}

			} while (!string.IsNullOrEmpty(current) && !isGitRoot);
			

			if (!string.IsNullOrEmpty(current) && isGitRoot)
			{
				var file = Path.Combine(current, "GitDepend.json");

				if (File.Exists(file))
				{
					try
					{
						var json = File.ReadAllText(file);
						var gitDependFile = JsonConvert.DeserializeObject<GitDependFile>(json);
						error = null;
						dir = current;
						gitDependFile.Build.Script = Path.GetFullPath(Path.Combine(current, gitDependFile.Build.Script));
						gitDependFile.Packages.Directory = Path.GetFullPath(Path.Combine(current, gitDependFile.Packages.Directory));

						foreach (var dependency in gitDependFile.Dependencies)
						{
							dependency.Directory = Path.GetFullPath(Path.Combine(current, dependency.Directory));
							string subdir;
							string suberror;
							dependency.Configuration = LoadFromDir(dependency.Directory, out subdir, out suberror);
						}
						return gitDependFile;
					}
					catch (Exception ex)
					{
						error = ex.Message;
						Console.Error.WriteLine(ex.Message);
						return null;
					}
				}
				error = null;
				return new GitDependFile();
			}

			error = "This is not a git repository";
			return null;
		}
	}
}