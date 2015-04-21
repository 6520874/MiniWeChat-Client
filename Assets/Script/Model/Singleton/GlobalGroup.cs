﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

using protocol;

namespace MiniWeChat
{
    public class GlobalGroup : Singleton<GlobalGroup>
    {
        private const float WAIT_QUERY_INTERVAL = 0.1f;

        private Dictionary<string, GroupItem> _groupDict = new Dictionary<string,GroupItem>();
        private Dictionary<string, UserItem> _groupMemberDict = new Dictionary<string, UserItem>();

        private HashSet<string> _waitQueryMemberSet = new HashSet<string>();

        public int Count
        {
            get { return _groupDict.Count; }
        }

        #region LifeCycle
        
        public override void Init()
        {

            base.Init();

            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.GET_PERSONALINFO_RSP, OnGetPersonalInfoRsp);
            
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)EModelMessage.TRY_LOGIN, OnTryLogin);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.LOGOUT_RSP, OnLogOutRsp);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.OFFLINE_SYNC, OnLogOutRsp);

            StartCoroutine(QueryMemberData());

            LoadGroupData();
        }

        public override void Release()
        {
            base.Release();

            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.GET_PERSONALINFO_RSP, OnGetPersonalInfoRsp);
            
            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)EModelMessage.TRY_LOGIN, OnTryLogin);
            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.LOGOUT_RSP, OnLogOutRsp);
            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.OFFLINE_SYNC, OnLogOutRsp);

            SaveAndClearGroupData();
        }

        #endregion

        #region QueryData

        public UserItem GetGroupMember(string userID)
        {
            if (!_groupMemberDict.ContainsKey(userID))
            {
                return _groupMemberDict[userID];
            }
            else 
	        {
                UserItem userItem = GlobalContacts.GetInstance().GetUserItemById(userID);

                if (userItem == null)
                {
                    _waitQueryMemberSet.Add(userID);
                }

                return userItem;
            }
        }

        public bool ContainsMember(string groupID, string userID)
        {
            GroupItem group = GetGroup(groupID);
            return group.memberUserId.Contains(groupID);
        }

        public GroupItem GetGroup(string groupID)
        {
            if (!_groupDict.ContainsKey(groupID))
            {
                return _groupDict[groupID];
            }
            else
            {
                throw new UnityException("No such group : " + groupID);
            }
        }

        private IEnumerator QueryMemberData()
        {
            while(true)
            {
                if (_waitQueryMemberSet.Count != 0)
                {
                    GetUserInfoReq req = new GetUserInfoReq();
                    foreach (var item in _waitQueryMemberSet)
                    {
                        req.targetUserId.Add(item);
                    }
                    NetworkManager.GetInstance().SendPacket<GetUserInfoReq>(ENetworkMessage.GET_USERINFO_REQ, req);
                    _waitQueryMemberSet.Clear();
                }
                yield return new WaitForSeconds(WAIT_QUERY_INTERVAL);
            }
        }

        #endregion

        #region MessageHandler

        public void OnGetPersonalInfoRsp(uint iMessageType, object kParam)
        {
            NetworkMessageParam param = kParam as NetworkMessageParam;
            GetPersonalInfoRsp rsp = param.rsp as GetPersonalInfoRsp;
            GetPersonalInfoReq req = param.req as GetPersonalInfoReq;
            if (rsp.resultCode == GetPersonalInfoRsp.ResultCode.SUCCESS
                && req.friendInfo)
            {
                _groupDict.Clear();
                foreach (GroupItem group in rsp.groups)
                {
                    _groupDict[group.groupId] = group;
                }
            }
        }

        public void OnTryLogin(uint iMessageType, object kParam)
        {
            LoadGroupData();
        }

        public void OnLogOutRsp(uint iMessageType, object kParam)
        {
            SaveAndClearGroupData();
        }

        #endregion

        #region LocalData

        private string GetGroupDirPath()
        {
            return GlobalUser.GetInstance().GetUserDir() + "/Group";
        }

        private void SaveGroupData()
        {
            foreach (var groupID in _groupDict.Keys)
            {
                string filePath = GetGroupDirPath() + "/" + groupID;
                IOTool.SerializeToFile<GroupItem>(filePath, _groupDict[groupID]);
            }
        }

        private void SaveAndClearGroupData()
        {
            SaveGroupData();
            ClearGroupData();
        }

        private void LoadGroupData()
        {
            if (_groupDict.Count == 0 && IOTool.IsDirExist(GetGroupDirPath()))
            {
                foreach (var file in IOTool.GetFiles(GetGroupDirPath()))
                {
                    GroupItem groupItem = IOTool.DeserializeFromFile<GroupItem>(file.FullName);
                    if (groupItem != null)
                    {
                        _groupDict[groupItem.groupId] = groupItem;
                    }
                }
            }
        }

        public void ClearGroupData()
        {
            _groupDict.Clear();
        }

        #endregion
    }
}
