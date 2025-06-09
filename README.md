# unity-observer

``` c#
    private void Awake() {
        SARSARWatcherBehaviourHelper.RegSARWatcherEvent(this, "test_event", (sender, arg) => {
            Debug.Log("this is call :" + arg[0]);
        });
    }

    private void Start() {
            
        onTestBtn.onClick.RemoveAllListeners();
        onTestBtn.onClick.AddListener(() => {
            SARSARWatcherBehaviourHelper.SARFireEvent(this, "test_event", onTestBtn.name);
        });
    }
