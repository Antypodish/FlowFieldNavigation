using UnityEngine;

public class EditorCameraController : MonoBehaviour
{
    [SerializeField] Transform _cameraPivotTransform;
    [SerializeField] Transform _cameraTransform;
    [Header("Movement Settings")]
    [SerializeField][Range(0, 20)] int _borderThickness;    //all good
    [SerializeField][Range(0, 5)] int _movementSensitivity; //1 good
    [Header("Zoom Settings")]
    [SerializeField] float _maxZoom; //-15 good
    [SerializeField] float _zoomSensitivity; //3 good

    float _camLocalTargetZ;
    float _camLocalStartZ;
    float _zoomDistanceZ;
    float _prevScrollDelta = 0f;

    Vector3 _movementTargetPosition;
    Cameramode _camMode = Cameramode.Gameplay;
    bool _canMove = true;

    private void Start()
    {
        _movementSensitivity *= 20;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightAlt))
        {
            SwitchCameraMode();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            _canMove = _canMove ? false : true;
        }
        if (_canMove)
        {
            UpdateMovement();
        }
        UpdateZoom(_cameraTransform.localPosition);
    }
    void UpdateMovement()
    {
        Vector3 movementAmount = Vector3.zero;

        if (Input.GetKey(KeyCode.UpArrow)
            || Input.mousePosition.y > Screen.height - _borderThickness)
        {
            movementAmount += MoveForward();
        }
        else if (Input.GetKey(KeyCode.DownArrow)
            || Input.mousePosition.y < _borderThickness)
        {
            movementAmount += MoveBackwards();
        }
        if (Input.GetKey(KeyCode.RightArrow)
            || Input.mousePosition.x > Screen.width - _borderThickness)
        {
            movementAmount += MoveRight();
        }
        else if (Input.GetKey(KeyCode.LeftArrow)
            || Input.mousePosition.x < _borderThickness)
        {
            movementAmount += MoveLeft();
        }
        _movementTargetPosition = _cameraPivotTransform.position + movementAmount;
        if (_cameraPivotTransform.position != _movementTargetPosition)
        {
            _cameraPivotTransform.position += movementAmount * Time.deltaTime;
        }

        Vector3 MoveForward()
        {
            return Vector3.forward * _movementSensitivity;
        }
        Vector3 MoveBackwards()
        {
            return Vector3.back * _movementSensitivity;
        }
        Vector3 MoveRight()
        {
            return Vector3.right * _movementSensitivity;
        }
        Vector3 MoveLeft()
        {
            return Vector3.left * _movementSensitivity;
        }
    }
    void UpdateZoom(Vector3 camLocalPos)
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if(scrollDelta != 0 && scrollDelta != _prevScrollDelta)
        {
            _prevScrollDelta = scrollDelta;
            _camLocalTargetZ = camLocalPos.z;
        }
        if(scrollDelta == -1)   //upwards
        {
            _camLocalTargetZ = _camLocalTargetZ + (scrollDelta * _zoomSensitivity);
            _camLocalStartZ = camLocalPos.z;
            _zoomDistanceZ = Mathf.Abs(_camLocalTargetZ - _camLocalStartZ);
        }
        if(scrollDelta == 1)    //downwards
        {
            _camLocalTargetZ = _camLocalTargetZ + (scrollDelta * _zoomSensitivity);
            _camLocalStartZ = camLocalPos.z;
            _zoomDistanceZ = Mathf.Abs(_camLocalTargetZ - _camLocalStartZ);
        }
        _camLocalTargetZ = Mathf.Clamp(_camLocalTargetZ, _maxZoom, 0f);

        float z = Mathf.MoveTowards(camLocalPos.z, _camLocalTargetZ, _zoomDistanceZ * Time.deltaTime * 4);
        _cameraTransform.localPosition = new Vector3(camLocalPos.x, camLocalPos.y, z);
    }
    void SwitchCameraMode()
    {
        if (_camMode == Cameramode.Gameplay)
        {
            _cameraPivotTransform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));
            _camMode = Cameramode.Perpendicular;
            return;
        }
        _cameraPivotTransform.rotation = Quaternion.Euler(new Vector3(57, 0, 0));
        _camMode = Cameramode.Gameplay;

    }
    enum Cameramode : byte
    {
        Gameplay,
        Perpendicular
    }
}