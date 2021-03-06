﻿using Domain.Entities;
using Logic.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhino.Mocks;
using Shared.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamMerge.Exceptions;
using TeamMerge.Helpers;
using TeamMerge.Operations;
using TeamMerge.Settings.Enums;
using Branch = TeamMerge.Settings.Enums.Branch;

namespace TeamMerge.Tests.Operations
{
    [TestClass]
    public class MergeOperationTests
    {
        private const string DO_GET_LATEST_ON_BRANCH_METHOD_NAME = "DoGetLatestOnBranchAsync";
        private const string CHECK_WORKSPACE_METHOD_NAME = "CheckIfWorkspaceHasIncludedPendingChangesAsync";
        private const string GETCOMMENT_METHOD_NAME = "GetCommentForMerge";

        private MergeOperation _sut;

        private IMergeService _mergeService;
        private IConfigManager _configManager;
        private ITeamExplorerService _teamExplorerService;

        private Workspace _currentWorkspaceModel;
        private string _sourceBranchName;
        private string _targetbranchName;
        
        [TestInitialize]
        public void Initialize()
        {
            _mergeService = MockRepository.GenerateStrictMock<IMergeService>();
            _configManager = MockRepository.GenerateStrictMock<IConfigManager>();
            _teamExplorerService = MockRepository.GenerateStrictMock<ITeamExplorerService>();

            _sut = new MergeOperation(_mergeService, _teamExplorerService, _configManager);

            _currentWorkspaceModel = new Workspace
            {
                OwnerName = "MyOwnerName",
                Name = "WorkspaceName"
            };

            _sourceBranchName = "SourceBranchName";
            _targetbranchName = "TargetBranchName";
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_CheckIfWorkspaceHasIncludedPendingChangesAsync_WhenCalledWithPendingChangesInWorkspace_ThenThrowsException()
        {
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.ENABLE_WARNING_WHEN_PENDING_CHANGES))
                .Return(true);

            _mergeService.Expect(x => x.HasIncludedPendingChanges(_currentWorkspaceModel))
                .Return(true);

            _sut.MyCurrentAction += InvokesMyCurrentActionWithCheckingPendingChanges;
            
            var obj = new PrivateObject(_sut);
            await (Task) obj.Invoke(CHECK_WORKSPACE_METHOD_NAME, _currentWorkspaceModel);
        }

        private void InvokesMyCurrentActionWithCheckingPendingChanges(object sender, string e)
        {
            Assert.AreEqual(Resources.CheckingPendingChanges, e);
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldNotResolveConflicts_ConflictsOccur_ThenThrowsException()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.Source);

            _configManager.Expect(x => x.GetValue<Branch>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return(Branch.Source);

            _mergeService.Expect(x => x.GetLatestVersionAsync(_currentWorkspaceModel, _sourceBranchName))
                .Return(Task.FromResult(true));

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(false);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldResolveConflicts_ButUserCancelsTheResolve_ThenThrowsException()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.Target);

            _configManager.Expect(x => x.GetValue<Branch>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return(Branch.Target);

            _mergeService.Expect(x => x.GetLatestVersionAsync(_currentWorkspaceModel, _targetbranchName))
                .Return(Task.FromResult(true));

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(true);

            _mergeService.Expect(x => x.ResolveConflictsAsync(_currentWorkspaceModel))
                .Return(Task.FromResult(0));

            _mergeService.Expect(x => x.HasConflicts(_currentWorkspaceModel))
                .Return(true);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        [TestMethod]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldResolveConflicts_AndEverythingGoesRight_ThenNothingHappens()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.SourceAndTarget);

            _configManager.Expect(x => x.GetValue<Branch>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return(Branch.SourceAndTarget);

            _mergeService.Expect(x => x.GetLatestVersionAsync(_currentWorkspaceModel, _targetbranchName, _sourceBranchName))
                .Return(Task.FromResult(true));

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(true);

            _mergeService.Expect(x => x.ResolveConflictsAsync(_currentWorkspaceModel))
                .Return(Task.CompletedTask);

            _mergeService.Expect(x => x.HasConflicts(_currentWorkspaceModel))
                .Return(false);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        private void ChecksIfMyCurrentActionIsCorrectlySet(Branch branch)
        {
            _sut.MyCurrentAction += (s, action) =>
            {
                Assert.IsTrue(action.Contains(branch.GetDescription().ToLower()));
            };
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsNone_ThenReturnsEmtpyString()
        {
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.None);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(string.Empty);

            var obj = new PrivateObject(_sut);
            var result = (string) obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, Enumerable.Empty<int>(), Enumerable.Empty<int>(), false);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsBranchDirection_ThenReturnsBranchDirectionString()
        {
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirection);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return("Merge {0} --> {1}");

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, Enumerable.Empty<int>(), Enumerable.Empty<int>(), false);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsBranchDirectionWithOnlySourceBranchInFormat_ThenReturnsBranchDirectionStringWithOnlySourceBranch()
        {
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirection);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return("Merge {0}");

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, Enumerable.Empty<int>(), Enumerable.Empty<int>(), false);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsFalse(result.Contains(_targetbranchName));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsBranchDirectionWithOnlyTargetBranchInFormat_ThenReturnsBranchDirectionStringWithOnlyTargetBranch()
        {
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirection);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return("Merge {1}");

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, Enumerable.Empty<int>(), Enumerable.Empty<int>(), false);

            Assert.IsFalse(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsWorkItemIds_ThenReturnsAllAssociatedWorkItemIds()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.WorkItemIds);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return("Merge work item ids: {0}");

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, Enumerable.Empty<int>(), false);

            Assert.IsTrue(result.Contains(string.Join(", ", workitemIds)));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsFixedComment_ThenReturnsFixedComment()
        {
            var awesomeCheckInComment = "Merging my things :o";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.Fixed);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, Enumerable.Empty<int>(), Enumerable.Empty<int>(), false);

            Assert.AreEqual(result, awesomeCheckInComment);
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsMergeDirectionAndWorkItems_ThenReturnsMergeDirectionWithWorkItems()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };
            var awesomeCheckInComment = "Merge: {0} --> {1} ({2})";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirectionAndWorkItems);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, Enumerable.Empty<int>(), false);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
            Assert.IsTrue(result.Contains(string.Join(", ", workitemIds)));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsMergeDirectionAndWorkItemsAndLatestVersionButShouldNotShow_ThenReturnsMergeDirectionWithWorkItems()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };
            var awesomeCheckInComment = "Merge: {0} --> {1} ({2})";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirectionAndWorkItems);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, Enumerable.Empty<int>(), true);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
            Assert.IsTrue(result.Contains(string.Join(", ", workitemIds)));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsMergeDirectionAndWorkItemsAndLatestVersionAndShouldShowComment_ThenReturnsMergeDirectionWithLatestVersion()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };
            var awesomeCheckInComment = "Merge: {0} --> {1} ({2})";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(true);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirectionAndWorkItems);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, Enumerable.Empty<int>(), true);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
            Assert.IsTrue(result.Contains(Resources.LatestVersion));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsChangesetIds_ThenReturnsChangesetIds()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };
            var changesetIds = new List<int> { 6, 23, 26, 27 };
            var awesomeCheckInComment = "Merged with changesetids: {0}";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.ChangesetIds);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, changesetIds, true);

            Assert.IsTrue(result.Contains(string.Join(", ", changesetIds)));
            Assert.IsFalse(result.Contains(string.Join(", ", workitemIds)));
        }

        [TestMethod]
        public void MergeOperation_GetCommentForMerge_WhenCalledAndCheckInCommentOptionIsMergeDirectionAndChangesetIds_ThenReturnsChangesetIds()
        {
            var workitemIds = new List<int> { 5, 10, 16, 18 };
            var changesetIds = new List<int> { 6, 23, 26, 27 };
            var awesomeCheckInComment = "Merge: {0} --> {1} ({2})";

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION)).Return(CheckInComment.MergeDirectionAndChangesetIds);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(awesomeCheckInComment);

            var obj = new PrivateObject(_sut);
            var result = (string)obj.Invoke(GETCOMMENT_METHOD_NAME, _sourceBranchName, _targetbranchName, workitemIds, changesetIds, true);

            Assert.IsTrue(result.Contains(_sourceBranchName));
            Assert.IsTrue(result.Contains(_targetbranchName));
            Assert.IsTrue(result.Contains(string.Join(", ", changesetIds)));
            Assert.IsFalse(result.Contains(string.Join(", ", workitemIds)));
        }

        [TestMethod]
        public async Task MergeOperation_Execute_WhenCalled_NothingGoesWrong()
        {
            var orderedChangesetIds = new List<int> { 2, 5, 7, 8 };

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.ENABLE_WARNING_WHEN_PENDING_CHANGES))
                .Return(false);

            _configManager.Expect(x => x.GetValue<Branch>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return(Branch.None);

            _sut.MyCurrentAction += (s, action) =>
            {
                Assert.AreEqual(Resources.MergingBranches, action);
            };

            var excludedWorkItemTypes = new List<string> { "Code Review Request" };

            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.EXCLUDE_WORK_ITEMS_FOR_MERGE)).Return(false);
            _configManager.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT)).Return(false);
            _configManager.Expect(x => x.GetValue<IEnumerable<string>>(ConfigKeys.WORK_ITEM_TYPES_TO_EXCLUDE))
                .Return(excludedWorkItemTypes);

            var workItemsToAdd = new List<int> { 5, 75, 85 };

            _configManager.Expect(x => x.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION))
                .Return(CheckInComment.None);
            _configManager.Expect(x => x.GetValue<string>(ConfigKeys.COMMENT_FORMAT)).Return(string.Empty);

            _mergeService.Expect(x => x.MergeBranchesAsync(_currentWorkspaceModel, _sourceBranchName, _targetbranchName, 2, 8)).Return(Task.CompletedTask);
            _mergeService.Expect(x => x.GetWorkItemIdsAsync(orderedChangesetIds, excludedWorkItemTypes)).Return(Task.FromResult<IEnumerable<int>>(workItemsToAdd));
            _teamExplorerService.Expect(x => x.AddWorkItemsAndCommentThenNavigate(_currentWorkspaceModel, string.Empty, workItemsToAdd));

            await _sut.ExecuteAsync(new MergeModel
            {
                OrderedChangesetIds = new List<int> { 2, 5, 7, 8 },
                SourceBranch = _sourceBranchName,
                TargetBranch = _targetbranchName,
                WorkspaceModel = _currentWorkspaceModel
            });
        }        

        [TestCleanup]
        public void CleanUp()
        {
            _mergeService.VerifyAllExpectations();
            _configManager.VerifyAllExpectations();
            _teamExplorerService.VerifyAllExpectations();
        }
    }
}
