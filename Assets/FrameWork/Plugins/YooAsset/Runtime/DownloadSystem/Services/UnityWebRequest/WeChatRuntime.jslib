mergeInto(LibraryManager.library, {
  Framework_IsWeChatRuntime: function () {
    return typeof wx !== 'undefined' && typeof wx.getSystemInfoSync === 'function'
      && !(typeof tt !== 'undefined' && typeof tt.getSystemInfoSync === 'function') ? 1 : 0;
  }
});
